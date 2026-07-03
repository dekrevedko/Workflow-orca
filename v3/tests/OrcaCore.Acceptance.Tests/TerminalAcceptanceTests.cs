using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

/// <summary>
/// T1-14 (CR-008, CR-016, CR-031, MG-004): public-API-only acceptance coverage for the
/// completion bridge, named End outcomes, graceful cancel, forced terminate, terminal-instance
/// lifecycle rejection, and the broad-destructive-selection safety gate.
/// </summary>
public class TerminalAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class CompleteStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildImmediateWorkflow(DefinitionId definitionId, string outcomeName = "Done")
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new CompleteStep());
        builder.End(outcomeName);
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildWaitingWorkflow(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new CompleteStep());
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Trait("AC", "AC-011")]
    [Fact]
    public async Task CompletionBridge_ReturnsTerminalSnapshotWithoutLiveState()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-ac011"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var completed = await engine.AwaitCompletionAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        completed.Should().BeOfType<WorkflowInstanceSnapshot>();
        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Trait("AC", "AC-012")]
    [Fact]
    public async Task NamedEndOutcome_IsRecordedAndQueryable()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-ac012"), "Approved");
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        started.EndOutcomeName.Should().Be("Approved");

        var queried = engine.Query().Instance(started.InstanceId).Get();
        queried!.EndOutcomeName.Should().Be("Approved");
    }

    [Trait("AC", "AC-014")]
    [Fact]
    public async Task GracefulCancel_CancelsInFlightWorkAndActiveWaits()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-ac014"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var cancelled = await engine.CancelAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        engine.Query().All().GetActiveWaits().Should().BeEmpty();
    }

    [Trait("AC", "AC-015")]
    [Fact]
    public async Task ForcedTerminate_PreventsFurtherAdvancement()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-ac015"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var terminated = await engine.TerminateAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);
        terminated.Status.Should().Be(WorkflowStatus.Terminated);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var outcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.NoMatch);
        engine.Query().Instance(started.InstanceId).Get()!.Status.Should().Be(WorkflowStatus.Terminated);
    }

    [Trait("AC", "AC-005")]
    [Fact]
    public async Task TerminalInstances_RejectIllegalTriggers()
    {
        var (engine, definitionId) = BuildImmediateWorkflow(new DefinitionId("terminal-ac005"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Completed);

        var envelope = new EventEnvelope(EventId.New(), "SomeEvent", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var raiseOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);
        raiseOutcome.Should().Be(RaiseEventOutcome.NoMatch);

        var cancelAct = async () => await engine.CancelAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);
        var terminateAct = async () => await engine.TerminateAsync<OrderState>(started.InstanceId, TestContext.Current.CancellationToken);

        await cancelAct.Should().ThrowAsync<WorkflowLifecycleException>();
        await terminateAct.Should().ThrowAsync<WorkflowLifecycleException>();
    }

    [Trait("AC", "AC-516")]
    [Fact]
    public async Task BroadDestructiveSelection_RequiresExplicitSafety()
    {
        var (engine, definitionId) = BuildWaitingWorkflow(new DefinitionId("terminal-ac516"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var unsafeAct = async () => await engine.Query().All().Terminate();
        await unsafeAct.Should().ThrowAsync<WorkflowDefinitionException>();

        engine.Query().Instance(started.InstanceId).Get()!.Status.Should().Be(WorkflowStatus.Waiting);

        var results = await engine.Query().All().Terminate(confirmBroadSelection: true, TestContext.Current.CancellationToken);

        results.Should().ContainSingle(result => result.InstanceId == started.InstanceId && result.Status == WorkflowStatus.Terminated);
    }
}
