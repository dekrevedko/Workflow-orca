using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class ParallelTests
{
    [Fact]
    public async Task Run_ParallelBranches_AllBranchesExecuteBeforeContinuation()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Then(() => new AppendStep("a"))),
                ("b", branch => branch.Then(() => new AppendStep("b"))))
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Values.Take(2).Should().BeEquivalentTo(["a", "b"]);
        state.Values.Last().Should().Be("after");
    }

    [Fact]
    public async Task Run_ParallelBranchesCompletingTogether_ContinuationRunsOnce()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);
        var second = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("B", new CorrelationId("b"), "b"),
            TestContext.Current.CancellationToken);
        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        state.ContinuationCount.Should().Be(1);
    }

    [Fact]
    public async Task Run_ParallelBranchesDifferentOrders_FinalStateIsDeterministic()
    {
        var first = await RunWaitingParallelAsync("A", "B");
        var second = await RunWaitingParallelAsync("B", "A");

        first.OrderBy(value => value).Should().Equal(second.OrderBy(value => value));
    }

    [Fact]
    public async Task Run_ParallelWithNoOpShapeChange_ContinuationBehaviorUnchanged()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Then(() => new AppendStep("a")).Then(() => new AppendStep("noop"))),
                ("b", branch => branch.Then(() => new AppendStep("b"))))
            .Then(() => new CountContinuationStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.ContinuationCount.Should().Be(1);
        state.Values.Should().Contain(["a", "b", "noop"]);
    }

    [Fact]
    public async Task RaiseEventAsync_ParallelBranchWait_ResumesOnlyMatchingBranch()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var stillWaiting = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("A", new CorrelationId("a"), "a"),
            TestContext.Current.CancellationToken);

        stillWaiting.Status.Should().Be(WorkflowStatus.Waiting);
        state.Values.Should().Equal(["a"]);
    }

    [Fact]
    public async Task Run_ParallelBranchWaits_RecordDistinctBranchIds()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Wait("Ready", _ => new CorrelationId("same"))),
                ("b", branch => branch.Wait("Ready", _ => new CorrelationId("same"))))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        waiting.ActiveWaits.Select(wait => wait.BranchId).Should().BeEquivalentTo(["0:a", "1:b"]);
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task RaiseEventAsync_ParallelBranchWaitsWithSameCorrelation_UsesBranchIdForDelivery()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = SameCorrelationWaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Ready", new CorrelationId("same"), "a", branchId: "0:a"),
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Ready", new CorrelationId("same"), "b", branchId: "1:b"),
            TestContext.Current.CancellationToken);

        first.Status.Should().Be(WorkflowStatus.Waiting);
        first.ActiveWaits.Should().ContainSingle(wait => wait.BranchId == "1:b");
        completed.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["a", "b", "after"]);
    }

    [Fact]
    [Trait("AC", "AC-110")]
    public async Task RaiseEventAsync_ParallelBranchWaitsWithSameCorrelation_RejectsAmbiguousDelivery()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = SameCorrelationWaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var act = () => engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Ready", new CorrelationId("same"), "a"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*multiple branch waits*BranchId*");
        state.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ParallelBranchCommits_RouteThroughInstanceLane()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        await Task.WhenAll(
            engine.RaiseEventAsync<TestState>(
                waiting.InstanceId,
                Event("A", new CorrelationId("a"), "a"),
                TestContext.Current.CancellationToken),
            engine.RaiseEventAsync<TestState>(
                waiting.InstanceId,
                Event("B", new CorrelationId("b"), "b"),
                TestContext.Current.CancellationToken)).WaitAsync(TestContext.Current.CancellationToken);

        state.ContinuationCount.Should().Be(1);
        state.Values.Should().BeEquivalentTo(["a", "b", "after"]);
    }

    private static async Task<IReadOnlyList<string>> RunWaitingParallelAsync(params string[] eventOrder)
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingParallelDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        foreach (var eventName in eventOrder)
        {
            await engine.RaiseEventAsync<TestState>(
                waiting.InstanceId,
                Event(eventName, new CorrelationId(eventName.ToLowerInvariant()), eventName.ToLowerInvariant()),
                TestContext.Current.CancellationToken);
        }

        return state.Values;
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingParallelDefinition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Wait("A", _ => new CorrelationId("a")).Then(() => new CaptureStep())),
                ("b", branch => branch.Wait("B", _ => new CorrelationId("b")).Then(() => new CaptureStep())))
            .Then(() => new CountContinuationStep())
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> SameCorrelationWaitingParallelDefinition(
        TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Parallel(
                ("a", branch => branch.Wait("Ready", _ => new CorrelationId("same")).Then(() => new CaptureStep())),
                ("b", branch => branch.Wait("Ready", _ => new CorrelationId("same")).Then(() => new CaptureStep())))
            .Then(() => new AppendStep("after"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(
        string name,
        CorrelationId correlationId,
        object? payload,
        string? branchId = null)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = name,
            CorrelationId = correlationId,
            BranchId = branchId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public List<string> Values { get; } = [];

        public int ContinuationCount { get; set; }
    }

    private sealed class AppendStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Values.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountContinuationStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.ContinuationCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
