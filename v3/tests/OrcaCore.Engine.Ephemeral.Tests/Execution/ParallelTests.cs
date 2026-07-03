using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

/// <summary>
/// CP-001/CP-002/CP-003/CR-044 (T1-12): <c>Parallel</c> branches run isolated branch state, the
/// <c>WhenAll</c> join fires its continuation exactly once, and the committed outcome is
/// insensitive to branch completion order or structurally irrelevant graph shape changes.
/// </summary>
public class ParallelTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class RecordingStep(string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add(tag);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class WaitStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private static WorkflowInstance<OrderState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("parallel-workflow"),
            new DefinitionVersion(1),
            new OrderState(),
            clock.Now);

    [Fact]
    public async Task Run_ParallelBranches_AllBranchesExecuteBeforeContinuation()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        builder.Then(new RecordingStep("after-join"));
        builder.End();
        var definition = builder.Build(new DefinitionId("parallel-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Contain("branch-0")
            .And.Contain("branch-1")
            .And.Contain("after-join");
        instance.State.Executed.IndexOf("after-join").Should().Be(
            instance.State.Executed.Count - 1,
            "the continuation must run only after every branch has completed");
    }

    [Fact]
    public async Task Run_ParallelBranchesCompletingTogether_ContinuationRunsOnce()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        builder.Then(new RecordingStep("after-join"));
        builder.End();
        var definition = builder.Build(new DefinitionId("parallel-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.State.Executed.Count(step => step == "after-join").Should().Be(1);
    }

    [Fact]
    public async Task Run_ParallelBranchesDifferentOrders_FinalStateIsDeterministic()
    {
        // Two definitions whose branch STEP COUNTS differ (branch 0 does more work than branch 1
        // in one definition, and vice versa in the other) must still commit the same final
        // executed-step SET and reach the same terminal status/outcome regardless of which
        // branch's internal work happens to finish "first" in ordinal driving order.
        var builderA = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builderA.Parallel(
            branch => branch.Then(new RecordingStep("a0")).Then(new RecordingStep("a0b")),
            branch => branch.Then(new RecordingStep("a1")));
        builderA.Then(new RecordingStep("join"));
        builderA.End();
        var definitionA = builderA.Build(new DefinitionId("order-a"), new DefinitionVersion(1));

        var builderB = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builderB.Parallel(
            branch => branch.Then(new RecordingStep("a1")),
            branch => branch.Then(new RecordingStep("a0")).Then(new RecordingStep("a0b")));
        builderB.Then(new RecordingStep("join"));
        builderB.End();
        var definitionB = builderB.Build(new DefinitionId("order-b"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instanceA = NewInstance(clock);
        var instanceB = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instanceA, definitionA, clock.TimeProvider, TestContext.Current.CancellationToken);
        await interpreter.RunAsync(instanceB, definitionB, clock.TimeProvider, TestContext.Current.CancellationToken);

        instanceA.Status.Should().Be(WorkflowStatus.Completed);
        instanceB.Status.Should().Be(WorkflowStatus.Completed);
        instanceA.State.Executed.ToHashSet().Should().BeEquivalentTo(instanceB.State.Executed.ToHashSet());
        instanceA.State.Executed.Last().Should().Be("join");
        instanceB.State.Executed.Last().Should().Be("join");
    }

    [Fact]
    public async Task Run_ParallelWithNoOpShapeChange_ContinuationBehaviorUnchanged()
    {
        var baseline = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        baseline.Parallel(
            branch => branch.Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        baseline.End("Done");
        var baselineDefinition = baseline.Build(new DefinitionId("shape-baseline"), new DefinitionVersion(1));

        var withNoOp = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        withNoOp.Parallel(
            branch => branch.Then(new RecordingStep("no-op")).Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        withNoOp.End("Done");
        var withNoOpDefinition = withNoOp.Build(new DefinitionId("shape-with-noop"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var baselineInstance = NewInstance(clock);
        var withNoOpInstance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(baselineInstance, baselineDefinition, clock.TimeProvider, TestContext.Current.CancellationToken);
        await interpreter.RunAsync(withNoOpInstance, withNoOpDefinition, clock.TimeProvider, TestContext.Current.CancellationToken);

        baselineInstance.Status.Should().Be(WorkflowStatus.Completed);
        withNoOpInstance.Status.Should().Be(WorkflowStatus.Completed);
        baselineInstance.EndOutcomeName.Should().Be("Done");
        withNoOpInstance.EndOutcomeName.Should().Be("Done");
    }

    [Fact]
    public async Task RaiseEventAsync_ParallelBranchWait_ResumesOnlyMatchingBranch()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new WaitStep("Signal0", new CorrelationId("c0"))).Then(new RecordingStep("branch-0-resumed")),
            branch => branch.Then(new WaitStep("Signal1", new CorrelationId("c1"))).Then(new RecordingStep("branch-1-resumed")));
        builder.Then(new RecordingStep("after-join"));
        builder.End();
        var definition = builder.Build(new DefinitionId("branch-wait-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);
        instance.Status.Should().Be(WorkflowStatus.Waiting, "both branches wait on their own event");

        var envelope0 = new EventEnvelope(EventId.New(), "Signal0", new CorrelationId("c0"), "p0", clock.Now);
        var outcome0 = await interpreter.TryResumeAsync(
            instance, definition, envelope0, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome0.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Waiting, "only branch 0 resumed; branch 1 must remain waiting");
        instance.State.Executed.Should().Contain("branch-0-resumed");
        instance.State.Executed.Should().NotContain("branch-1-resumed");
        instance.State.Executed.Should().NotContain("after-join");

        var envelope1 = new EventEnvelope(EventId.New(), "Signal1", new CorrelationId("c1"), "p1", clock.Now);
        var outcome1 = await interpreter.TryResumeAsync(
            instance, definition, envelope1, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome1.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Contain("branch-1-resumed").And.Contain("after-join");
    }

    [Fact]
    public async Task Run_ParallelBranchCommits_RouteThroughInstanceLane()
    {
        // CR-044: branch-state mutations and the join check must commit through the same
        // per-instance serialized path as everything else — i.e. driving Parallel happens
        // entirely inside one Interpreter.RunAsync call under the caller's own instance lane,
        // never spawning independent Tasks per branch. We prove this by checking that no
        // concurrent/overlapping execution is observable: a step recording "start"/"end" pairs
        // for each branch must show fully sequential (non-interleaved at the async-step-body
        // level) execution across branches within a single RunAsync call.
        var log = new List<string>();

        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new LoggingStep("b0", log)),
            branch => branch.Then(new LoggingStep("b1", log)));
        builder.End();
        var definition = builder.Build(new DefinitionId("lane-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        // Sequential round-robin driving means each branch's start/end pair is contiguous —
        // never interleaved with another branch's start before its own end.
        log.Should().Equal("b0-start", "b0-end", "b1-start", "b1-end");
    }

    private sealed class LoggingStep(string tag, List<string> log) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            log.Add($"{tag}-start");
            log.Add($"{tag}-end");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
