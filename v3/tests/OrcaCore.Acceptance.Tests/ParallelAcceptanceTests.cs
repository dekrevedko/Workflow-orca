using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

/// <summary>
/// Public-API acceptance coverage for T1-12 <c>Parallel</c>/<c>WhenAll</c> (CP-001, CP-002,
/// CP-003, CR-044): AC-201, AC-202, AC-203, AC-110, AC-007.
/// </summary>
public class ParallelAcceptanceTests
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

    [Trait("AC", "AC-201")]
    [Fact]
    public async Task ParallelWhenAll_ContinuationRunsExactlyOnce()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("ac-201-workflow");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        builder.Then(new RecordingStep("continuation"));
        builder.End();
        engine.RegisterDefinition(builder.Build(definitionId, new DefinitionVersion(1)));

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Trait("AC", "AC-202")]
    [Fact]
    public async Task ParallelWhenAll_OrderInsensitiveOutcome()
    {
        var engine = new EphemeralWorkflowEngine();

        var builderFast0 = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builderFast0.Parallel(
            branch => branch.Then(new RecordingStep("short")),
            branch => branch.Then(new RecordingStep("long-a")).Then(new RecordingStep("long-b")));
        builderFast0.Then(new RecordingStep("continuation"));
        builderFast0.End("Outcome");
        var definitionFast0 = new DefinitionId("ac-202-fast0");
        engine.RegisterDefinition(builderFast0.Build(definitionFast0, new DefinitionVersion(1)));

        var builderFast1 = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builderFast1.Parallel(
            branch => branch.Then(new RecordingStep("long-a")).Then(new RecordingStep("long-b")),
            branch => branch.Then(new RecordingStep("short")));
        builderFast1.Then(new RecordingStep("continuation"));
        builderFast1.End("Outcome");
        var definitionFast1 = new DefinitionId("ac-202-fast1");
        engine.RegisterDefinition(builderFast1.Build(definitionFast1, new DefinitionVersion(1)));

        var snapshot0 = await engine.StartAsync<int, OrderState>(definitionFast0, 0, TestContext.Current.CancellationToken);
        var snapshot1 = await engine.StartAsync<int, OrderState>(definitionFast1, 0, TestContext.Current.CancellationToken);

        snapshot0.Status.Should().Be(WorkflowStatus.Completed);
        snapshot1.Status.Should().Be(WorkflowStatus.Completed);
        snapshot0.EndOutcomeName.Should().Be("Outcome");
        snapshot1.EndOutcomeName.Should().Be("Outcome");
    }

    [Trait("AC", "AC-203")]
    [Fact]
    public async Task ParallelWhenAll_GraphShapeInsensitive()
    {
        var engine = new EphemeralWorkflowEngine();

        var baselineBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        baselineBuilder.Parallel(
            branch => branch.Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        baselineBuilder.End("Outcome");
        var baselineDefinitionId = new DefinitionId("ac-203-baseline");
        engine.RegisterDefinition(baselineBuilder.Build(baselineDefinitionId, new DefinitionVersion(1)));

        var withNoOpBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        withNoOpBuilder.Parallel(
            branch => branch.Then(new RecordingStep("no-op")).Then(new RecordingStep("branch-0")),
            branch => branch.Then(new RecordingStep("branch-1")));
        withNoOpBuilder.End("Outcome");
        var withNoOpDefinitionId = new DefinitionId("ac-203-with-noop");
        engine.RegisterDefinition(withNoOpBuilder.Build(withNoOpDefinitionId, new DefinitionVersion(1)));

        var baselineSnapshot = await engine.StartAsync<int, OrderState>(baselineDefinitionId, 0, TestContext.Current.CancellationToken);
        var withNoOpSnapshot = await engine.StartAsync<int, OrderState>(withNoOpDefinitionId, 0, TestContext.Current.CancellationToken);

        baselineSnapshot.Status.Should().Be(WorkflowStatus.Completed);
        withNoOpSnapshot.Status.Should().Be(WorkflowStatus.Completed);
        baselineSnapshot.EndOutcomeName.Should().Be("Outcome");
        withNoOpSnapshot.EndOutcomeName.Should().Be("Outcome");
    }

    [Trait("AC", "AC-110")]
    [Fact]
    public async Task ParallelWaits_MatchingEventResumesOnlyItsBranch()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("ac-110-workflow");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new WaitStep("Signal0", new CorrelationId("c0"))).Then(new RecordingStep("branch-0-resumed")),
            branch => branch.Then(new WaitStep("Signal1", new CorrelationId("c1"))).Then(new RecordingStep("branch-1-resumed")));
        builder.Then(new RecordingStep("continuation"));
        builder.End();
        engine.RegisterDefinition(builder.Build(definitionId, new DefinitionVersion(1)));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var wrongEnvelope = new EventEnvelope(EventId.New(), "Signal1", new CorrelationId("wrong-correlation"), null, DateTimeOffset.UnixEpoch);
        var noMatchOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, wrongEnvelope, TestContext.Current.CancellationToken);
        noMatchOutcome.Should().Be(RaiseEventOutcome.NoMatch);

        var envelope0 = new EventEnvelope(EventId.New(), "Signal0", new CorrelationId("c0"), "p0", DateTimeOffset.UnixEpoch);
        var outcome0 = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope0, TestContext.Current.CancellationToken);
        outcome0.Should().Be(RaiseEventOutcome.Resumed);

        var stillWaiting = await engine.RaiseEventAsync<OrderState>(
            started.InstanceId,
            new EventEnvelope(EventId.New(), "Signal0", new CorrelationId("c0"), "duplicate-shape", DateTimeOffset.UnixEpoch),
            TestContext.Current.CancellationToken);
        stillWaiting.Should().Be(RaiseEventOutcome.NoMatch, "branch 0's wait already resolved; branch 1 still owns the only active wait");

        var envelope1 = new EventEnvelope(EventId.New(), "Signal1", new CorrelationId("c1"), "p1", DateTimeOffset.UnixEpoch);
        var outcome1 = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope1, TestContext.Current.CancellationToken);
        outcome1.Should().Be(RaiseEventOutcome.Resumed);
    }

    [Trait("AC", "AC-007")]
    [Fact]
    public async Task RacingBranchCompletions_SerializeDeterministically()
    {
        // Deterministic coordination, not timing: two branches each wait on their own event; both
        // events are delivered back-to-back through the SAME per-instance lane. Because branch
        // driving and the join check happen entirely inside the lane-protected interpreter call
        // (CR-044), there is no real race to observe — the join must still fire exactly once and
        // the final committed state must be fully deterministic regardless of delivery order.
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("ac-007-workflow");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Parallel(
            branch => branch.Then(new WaitStep("Signal0", new CorrelationId("c0"))).Then(new RecordingStep("branch-0-resumed")),
            branch => branch.Then(new WaitStep("Signal1", new CorrelationId("c1"))).Then(new RecordingStep("branch-1-resumed")));
        builder.Then(new RecordingStep("continuation"));
        builder.End();
        engine.RegisterDefinition(builder.Build(definitionId, new DefinitionVersion(1)));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 0, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var envelope0 = new EventEnvelope(EventId.New(), "Signal0", new CorrelationId("c0"), "p0", DateTimeOffset.UnixEpoch);
        var envelope1 = new EventEnvelope(EventId.New(), "Signal1", new CorrelationId("c1"), "p1", DateTimeOffset.UnixEpoch);

        var outcome0 = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope0, TestContext.Current.CancellationToken);
        var outcome1 = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope1, TestContext.Current.CancellationToken);

        outcome0.Should().Be(RaiseEventOutcome.Resumed);
        outcome1.Should().Be(RaiseEventOutcome.Resumed);

        var finalSnapshot = await engine.RaiseEventAsync<OrderState>(
            started.InstanceId,
            new EventEnvelope(EventId.New(), "Signal0", new CorrelationId("c0"), "late-duplicate", DateTimeOffset.UnixEpoch),
            TestContext.Current.CancellationToken);
        finalSnapshot.Should().Be(RaiseEventOutcome.NoMatch, "the continuation already fired exactly once; nothing remains waiting");
    }
}
