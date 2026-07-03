using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Sagas;

public sealed class ChildCompensationTests
{
    [Fact]
    [Trait("AC", "AC-616")]
    public void CompensateChildGroup_SpawnsOneCompensationPerCompletedChild()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ChildrenScheduled(),
                ChildCompleted(ChildId(0), 3),
                ChildCompleted(ChildId(1), 4)
            ]);

        var decision = aggregate.DecideCompensateChildGroup(CompensateChildren(5));

        var scheduled = decision.Events.OfType<WorkflowChildCompensationScheduledEvent>().Should().ContainSingle().Subject;
        scheduled.GroupId.Should().Be(GroupId());
        scheduled.Compensations.Select(compensation => compensation.SourceChildInstanceId)
            .Should().Equal(ChildId(0), ChildId(1));
        scheduled.Compensations.Select(compensation => compensation.CompensationInstanceId)
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("AC", "AC-616")]
    public void CancelChildGroup_DoesNotTriggerCompensation()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ChildrenScheduled(),
                ChildCompleted(ChildId(0), 3)
            ]);

        var decision = aggregate.DecideCancel(new CancelWorkflowCommand
        {
            CommandId = CommandIdValue(4),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(4)
        });

        decision.Events.OfType<WorkflowChildCompensationScheduledEvent>().Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-616")]
    public void RepeatChildGroupCompensation_IsIdempotent()
    {
        var first = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ChildrenScheduled(),
                ChildCompleted(ChildId(0), 3)
            ]).DecideCompensateChildGroup(CompensateChildren(4));
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ChildrenScheduled(),
                ChildCompleted(ChildId(0), 3),
                .. first.Events
            ]);

        var second = aggregate.DecideCompensateChildGroup(CompensateChildren(5));

        second.Events.OfType<WorkflowChildCompensationScheduledEvent>().Should().BeEmpty();
    }

    private static WorkflowStartedEvent Started()
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            InstanceId = InstanceIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowChildrenScheduledEvent ChildrenScheduled()
    {
        return new WorkflowChildrenScheduledEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            GroupId = GroupId(),
            ChildDefinitionId = DefinitionIdValue(2),
            ChildDefinitionVersion = DefinitionVersion.Initial,
            FailurePolicy = RunChildFailurePolicy.PropagateFailure,
            JoinPolicy = RunChildrenJoinPolicy.WhenAll,
            ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
            TotalItemCount = 3,
            InitialDispatchCount = 3,
            NextDispatchIndex = 3,
            Children =
            [
                Child(0, "alpha"),
                Child(1, "beta"),
                Child(2, "gamma")
            ]
        };
    }

    private static WorkflowChildMaterialization Child(int index, string itemSnapshot)
    {
        return new WorkflowChildMaterialization
        {
            Index = index,
            ChildInstanceId = ChildId(index),
            ItemSnapshot = itemSnapshot
        };
    }

    private static WorkflowChildCompletedEvent ChildCompleted(InstanceId childId, int commandValue)
    {
        return new WorkflowChildCompletedEvent
        {
            EventId = EventIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(commandValue),
            CausationId = CausationIdValue(commandValue),
            OccurredAt = Timestamp(commandValue),
            ChildInstanceId = childId,
            ChildStatus = WorkflowStatus.Completed
        };
    }

    private static CompensateChildGroupCommand CompensateChildren(int commandValue)
    {
        return new CompensateChildGroupCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            GroupId = GroupId(),
            CompensationDefinitionId = DefinitionIdValue(9),
            CompensationDefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static string GroupId()
    {
        return CommandIdValue(2).Value.ToString("D");
    }

    private static InstanceId ChildId(int index)
    {
        return InstanceIdValue(20 + index);
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 18, minutes, 0, TimeSpan.Zero);
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
