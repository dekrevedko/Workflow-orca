using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class SagaPolicyInteractionTests
{
    [Fact]
    [Trait("AC", "AC-405")]
    public void ForwardTimeout_WhenPolicyRequiresCompensation_RecordsCompensationPlan()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), ForwardCompleted("reserve", "release")]);

        var decision = aggregate.DecideSagaForwardActionTimedOut(
            new SagaForwardActionTimedOutCommand
            {
                CommandId = CommandIdValue(2),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(2),
                ScopeId = "checkout",
                ActionKey = "authorize",
                CompensateScope = true
            });

        decision.Events.OfType<SagaForwardActionTimedOutEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("authorize");
        decision.Events.OfType<SagaCompensationRequestedEvent>().Should().ContainSingle()
            .Which.Reason.Should().Be("timeout");
        decision.Events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("release");
    }

    [Fact]
    [Trait("AC", "AC-405")]
    public void SagaCancellation_NeverTriggersCompensation()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), ForwardCompleted("reserve", "release")]);

        var decision = aggregate.DecideCancel(new CancelWorkflowCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(2)
        });

        decision.Events.OfType<SagaCompensationRequestedEvent>().Should().BeEmpty();
        decision.Events.OfType<SagaCompensationStartedEvent>().Should().BeEmpty();
        decision.Events.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowStatus.Cancelled);
    }

    private static WorkflowStartedEvent Started()
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static SagaForwardActionCompletedEvent ForwardCompleted(
        string actionKey,
        string compensationKey)
    {
        return new SagaForwardActionCompletedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            ScopeId = "checkout",
            ActionKey = actionKey,
            CompensationKey = compensationKey
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 14, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
