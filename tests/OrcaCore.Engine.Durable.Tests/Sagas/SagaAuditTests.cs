using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class SagaAuditTests
{
    [Fact]
    [Trait("AC", "AC-407")]
    public async Task SagaAudit_AfterCompensation_IncludesForwardCompensationOrderAndOutcome()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RequestCompensation(3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(CompleteCompensation("release", 4), TestContext.Current.CancellationToken);

        var audit = await new DurableManagement(store)
            .GetSagaAuditAsync(InstanceIdValue(1), TestContext.Current.CancellationToken);

        var scope = audit.Scopes.Should().ContainSingle().Subject;
        scope.ScopeId.Should().Be("checkout");
        scope.Outcome.Should().Be(WorkflowStatus.Compensated);
        scope.ForwardActions.Should().ContainSingle().Which.Should().BeEquivalentTo(new SagaForwardActionSnapshot
        {
            ScopeId = "checkout",
            ActionKey = "reserve",
            CompensationKey = "release",
            CompletedAt = Timestamp(2)
        });
        scope.CompensationActions.Should().ContainSingle().Which.Should().BeEquivalentTo(new SagaCompensationActionSnapshot
        {
            ScopeId = "checkout",
            ActionKey = "release",
            Order = 0,
            StartedAt = Timestamp(3),
            CompletedAt = Timestamp(4),
            Status = SagaCompensationActionStatus.Completed
        });
    }

    [Fact]
    [Trait("AC", "AC-408")]
    public async Task ManualRecovery_OnCompensationFailed_RecordsOperatorIntervention()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(RequestCompensation(3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(FailCompensation("release", 4), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(new RecordSagaManualRecoveryCommand
        {
            CommandId = CommandIdValue(5),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(5),
            ScopeId = "checkout",
            ActionKey = "release",
            OperatorId = "ops-1",
            RecoveryAction = "manual-resolution",
            Reason = "operator confirmed external refund",
            TargetStatus = WorkflowStatus.Compensated
        }, TestContext.Current.CancellationToken);

        var audit = await new DurableManagement(store)
            .GetSagaAuditAsync(InstanceIdValue(1), TestContext.Current.CancellationToken);

        var intervention = audit.Scopes.Should().ContainSingle().Subject
            .RecoveryInterventions.Should().ContainSingle().Subject;
        intervention.Should().BeEquivalentTo(new SagaRecoveryInterventionSnapshot
        {
            ScopeId = "checkout",
            ActionKey = "release",
            OperatorId = "ops-1",
            RecoveryAction = "manual-resolution",
            Reason = "operator confirmed external refund",
            RecordedAt = Timestamp(5),
            TargetStatus = WorkflowStatus.Compensated
        });
    }

    [Fact]
    [Trait("AC", "AC-406")]
    public async Task SagaRestart_MidCompensation_DoesNotDuplicateActions()
    {
        var store = new InMemoryWorkflowProvider();
        var beforeRestart = new DurableCommandProcessor(store);
        await beforeRestart.ProcessAsync(Start(), TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(ForwardCompleted("reserve", "release", 2), TestContext.Current.CancellationToken);
        await beforeRestart.ProcessAsync(RequestCompensation(3), TestContext.Current.CancellationToken);

        var afterRestart = new DurableCommandProcessor(store);
        await afterRestart.ProcessAsync(RequestCompensation(4), TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(InstanceIdValue(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("release");
    }

    private static StartWorkflowCommand Start()
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static RecordSagaForwardActionCompletedCommand ForwardCompleted(
        string actionKey,
        string compensationKey,
        int commandValue)
    {
        return new RecordSagaForwardActionCompletedCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            ActionKey = actionKey,
            CompensationKey = compensationKey
        };
    }

    private static RequestSagaCompensationCommand RequestCompensation(int commandValue)
    {
        return new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            Reason = "forward failure"
        };
    }

    private static CompleteSagaCompensationCommand CompleteCompensation(string actionKey, int commandValue)
    {
        return new CompleteSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            ActionKey = actionKey
        };
    }

    private static FailSagaCompensationCommand FailCompensation(string actionKey, int commandValue)
    {
        return new FailSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout",
            ActionKey = actionKey,
            ErrorSummary = "refund failed"
        };
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 17, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
