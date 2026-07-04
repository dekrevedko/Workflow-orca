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

public sealed class CoreRuntimeScenarioTests
{
    [Fact]
    [Trait("Scenario", "NEG-CR-001")]
    public async Task NEG_CR_001_StartUnknownDefinition_ThrowsDefinitionException()
    {
        var engine = new EphemeralWorkflowEngine();
        var unknownDefinitionId = DefinitionId.New();

        var act = async () => await engine.StartAsync<string, TestState>(
            unknownDefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowDefinitionException>()
            .WithMessage($"*{unknownDefinitionId}*");
    }

    [Fact]
    [Trait("Scenario", "NEG-CR-004")]
    [Trait("AC", "AC-011")]
    public async Task NEG_CR_004_AwaitCompletionAsync_NonTerminalWorkflowThrowsLifecycleException()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);

        var act = async () => await engine.AwaitCompletionAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*did not reach a terminal status synchronously*");
    }

    [Fact]
    [Trait("Scenario", "NEG-EV-001")]
    [Trait("AC", "EV-010")]
    public async Task NEG_EV_001_RaiseEventAsync_UnknownInstanceThrowsRoutingException()
    {
        var engine = new EphemeralWorkflowEngine();
        var unknownInstanceId = InstanceId.New();

        var act = async () => await engine.RaiseEventAsync<TestState>(
            unknownInstanceId,
            Event("Ready", "missing"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage($"*{unknownInstanceId}*");
    }

    [Fact]
    [Trait("Scenario", "EDGE-EV-009")]
    [Trait("AC", "EV-010")]
    public async Task EDGE_EV_009_RaiseEventByDefinitionAsync_FanoutResumesEachMatchingInstanceOnce()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var first = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "shared",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "shared",
            TestContext.Current.CancellationToken);

        var snapshots = await engine.RaiseEventByDefinitionAsync<TestState>(
            definition.DefinitionId,
            Event("Ready", "shared"),
            TestContext.Current.CancellationToken);

        snapshots.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([first.InstanceId, second.InstanceId]);
        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
        engine.Management.All().GetActiveWaits().Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-001")]
    [Trait("AC", "MG-001")]
    public void NEG_MG_001_AllList_EmptyRegistryReturnsEmptyListAndCount()
    {
        var engine = new EphemeralWorkflowEngine();

        var query = engine.Management.All();

        query.List().Should().BeEmpty();
        query.Count().Should().Be(0);
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-003")]
    [Trait("AC", "AC-502")]
    public async Task NEG_MG_003_ConfirmedTerminate_EmptySelectionReturnsZeroAffected()
    {
        var engine = new EphemeralWorkflowEngine();

        var report = await engine.Management.All()
            .Where(instance => instance.Status == WorkflowStatus.Waiting)
            .TerminateAsync(DestructiveCommandSafety.Confirmed, TestContext.Current.CancellationToken);

        report.AffectedCount.Should().Be(0);
        report.Results.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-020")]
    [Trait("AC", "MG-002")]
    public async Task NEG_MG_020_CancelMissingInstanceThrowsRoutingException()
    {
        var engine = new EphemeralWorkflowEngine();
        var unknownInstanceId = InstanceId.New();

        var act = async () => await engine.Management.Instance(unknownInstanceId)
            .CancelAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage($"*{unknownInstanceId}*");
    }

    [Fact]
    [Trait("Scenario", "NEG-CR-017")]
    [Trait("AC", "AC-001")]
    public void NEG_CR_017_RegisterDefinitionWithDurableOnlyNodes_ThrowsDefinitionException()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { CorrelationId = input })
            .RunChild(DefinitionId.New(), DefinitionVersion.Initial)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var act = () => engine.RegisterDefinition(definition);

        act.Should().Throw<WorkflowDefinitionException>()
            .WithMessage("*durable-only*");
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { CorrelationId = input })
            .Wait("Ready", state => new CorrelationId(state.CorrelationId))
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
            Payload = "payload",
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public string CorrelationId { get; init; } = string.Empty;
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
