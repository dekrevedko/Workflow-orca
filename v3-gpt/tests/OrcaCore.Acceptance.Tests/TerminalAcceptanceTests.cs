using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class TerminalAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-011")]
    public async Task CompletionBridge_ReturnsTerminalSnapshotWithoutLiveState()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.AwaitCompletionAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-012")]
    public async Task NamedEndOutcome_IsRecordedAndQueryable()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .End("Approved")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        engine.Management.All()
            .Where(instance => instance.EndOutcomeName == "Approved")
            .List()
            .Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-014")]
    public async Task GracefulCancel_CancelsInFlightWorkAndActiveWaits()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var cancelled = await engine.Management.Instance(started.InstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);

        cancelled.Status.Should().Be(WorkflowStatus.Cancelled);
        cancelled.ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public async Task ForcedTerminate_PreventsFurtherAdvancement()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        await engine.Management.Instance(started.InstanceId)
            .TerminateAsync(TestContext.Current.CancellationToken);
        var act = () => engine.RaiseEventAsync<TestState>(
            started.InstanceId,
            Event("Ready", "wait"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*terminal*");
    }

    [Fact]
    [Trait("AC", "AC-005")]
    public async Task TerminalInstances_RejectIllegalTriggers()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var completed = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        var act = () => engine.Management.Instance(completed.InstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*Cancel*Completed*");
    }

    [Fact]
    [Trait("AC", "AC-516")]
    public async Task BroadDestructiveSelection_RequiresExplicitSafety()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var unsafeTerminate = () => engine.Management.All()
            .TerminateAsync(TestContext.Current.CancellationToken);
        var safeTerminate = await engine.Management.All()
            .TerminateAsync(DestructiveCommandSafety.Confirmed, TestContext.Current.CancellationToken);

        await unsafeTerminate.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*explicit safety*");
        safeTerminate.AffectedCount.Should().Be(1);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> CompletedDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => new CorrelationId(state.Name))
            .Then(() => new CaptureStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(string eventName, string correlationId)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = eventName,
            CorrelationId = new CorrelationId(correlationId),
            Payload = null,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CaptureStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
