using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class CompensationFailureTests
{
    [Fact]
    [Trait("AC", "AC-404")]
    public void CompensationFailure_EndsSagaAsCompensationFailed()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), CompensationStarted()]);

        var decision = aggregate.DecideFailSagaCompensation(
            new FailSagaCompensationCommand
            {
                CommandId = CommandIdValue(3),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(3),
                ScopeId = "checkout",
                ActionKey = "release",
                ErrorSummary = "release failed"
            });

        decision.Events[0].Should().BeOfType<SagaCompensationFailedEvent>()
            .Which.ErrorSummary.Should().Be("release failed");
        decision.Events[1].Should().BeOfType<WorkflowTerminalEvent>()
            .Which.Status.Should().Be(WorkflowStatus.CompensationFailed);
    }

    [Fact]
    public void CompensationCompletion_EndsSagaAsCompensated()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), CompensationStarted()]);

        var decision = aggregate.DecideCompleteSagaCompensation(
            new CompleteSagaCompensationCommand
            {
                CommandId = CommandIdValue(3),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(3),
                ScopeId = "checkout",
                ActionKey = "release"
            });

        decision.Events[0].Should().BeOfType<SagaCompensationCompletedEvent>();
        decision.Events[1].Should().BeOfType<WorkflowTerminalEvent>()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
    }

    [Fact]
    public void MultipleCompensationCompletions_TerminateOnlyAfterAllStartedActionsComplete()
    {
        var firstStarted = CompensationStarted("release-inventory", 0);
        var secondStarted = CompensationStarted("void-payment", 1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), firstStarted, secondStarted]);

        var first = aggregate.DecideCompleteSagaCompensation(
            new CompleteSagaCompensationCommand
            {
                CommandId = CommandIdValue(3),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(3),
                ScopeId = "checkout",
                ActionKey = "release-inventory"
            });
        var replayed = DurableWorkflowAggregate.Rehydrate(null, [Started(), firstStarted, secondStarted, .. first.Events]);
        var second = replayed.DecideCompleteSagaCompensation(
            new CompleteSagaCompensationCommand
            {
                CommandId = CommandIdValue(4),
                InstanceId = InstanceIdValue(1),
                RequestedAt = Timestamp(4),
                ScopeId = "checkout",
                ActionKey = "void-payment"
            });

        first.Events.OfType<WorkflowTerminalEvent>().Should().BeEmpty();
        second.Events.OfType<WorkflowTerminalEvent>().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
    }

    [Fact]
    [Trait("AC", "AC-409")]
    public void RepeatedCompensationRequest_DoesNotDuplicateCompensationFacts()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), ForwardCompleted("reserve", "release")]);
        var first = aggregate.DecideRequestSagaCompensation(RequestCompensation(2));
        var replayed = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), ForwardCompleted("reserve", "release"), .. first.Events]);

        var second = replayed.DecideRequestSagaCompensation(RequestCompensation(3));

        second.Events.Should().BeEmpty();
    }

    private static RequestSagaCompensationCommand RequestCompensation(int commandValue)
    {
        return new RequestSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "checkout"
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

    private static SagaCompensationStartedEvent CompensationStarted()
    {
        return CompensationStarted("release", 0);
    }

    private static SagaCompensationStartedEvent CompensationStarted(string actionKey, int order)
    {
        return new SagaCompensationStartedEvent
        {
            EventId = EventIdValue(order + 2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            ScopeId = "checkout",
            ActionKey = actionKey,
            Order = order
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 13, 0, seconds, TimeSpan.Zero);
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
