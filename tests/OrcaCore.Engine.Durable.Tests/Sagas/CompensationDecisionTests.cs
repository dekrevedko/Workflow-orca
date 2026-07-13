using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class CompensationDecisionTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    public void SagaSuccess_WhenNoFailure_CompletesWithoutCompensation()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);

        var decision = aggregate.DecideComplete(
            new DurableCompleteCommand(CommandIdValue(2), InstanceIdValue(1), Timestamp(2), null));

        decision.Events.OfType<SagaCompensationRequestedEvent>().Should().BeEmpty();
        decision.Events.OfType<SagaCompensationStartedEvent>().Should().BeEmpty();
        decision.Events.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-402")]
    public void SagaFailure_AfterForwardActions_RecordsCompensationPlan()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), ForwardCompleted("reserve", "release")]);

        var decision = aggregate.DecideRequestSagaCompensation(
            RequestCompensation(2));

        decision.Events.OfType<SagaCompensationRequestedEvent>().Should().ContainSingle()
            .Which.ScopeId.Should().Be("checkout");
        decision.Events.OfType<SagaCompensationStartedEvent>().Should().ContainSingle()
            .Which.ActionKey.Should().Be("release");
    }

    [Fact]
    [Trait("AC", "AC-403")]
    public void CompensationPlan_UsesReverseSuccessfulCompletionOrder()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ForwardCompleted("reserve", "release"),
                ForwardCompleted("authorize", "refund")
            ]);

        var decision = aggregate.DecideRequestSagaCompensation(
            RequestCompensation(2));

        decision.Events.OfType<SagaCompensationStartedEvent>()
            .Select(compensation => compensation.ActionKey)
            .Should().Equal("refund", "release");
        decision.Events.OfType<SagaCompensationStartedEvent>()
            .Select(compensation => compensation.Order)
            .Should().Equal(0, 1);
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
            EventId = EventId.New(),
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
        return new DateTimeOffset(2026, 7, 2, 12, 30, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
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
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
