using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableChildWorkflowStateTests
{
    [Fact]
    public void PlanChildCompletion_WhenAllThrottledGroupHasCapacity_DispatchesNextChildWithoutResume()
    {
        var group = ActiveGroup(
            maxConcurrency: 2,
            nextDispatchIndex: 2,
            children: [Child(0), Child(1), Child(2)]);
        var state = DurableChildWorkflowState.FromSnapshot(
            [
                ActiveChild(0, group),
                ActiveChild(1, group)
            ],
            [group],
            [],
            [],
            [],
            []);

        var plan = state.PlanChildCompletion(
            ChildCompletionContext(CommandIdValue(10), Timestamp(10)),
            ChildCompleted(ChildId(0), WorkflowStatus.Completed));

        plan.Should().NotBeNull();
        plan!.ShouldFailParent.Should().BeFalse();
        plan.Events.OfType<WorkflowChildrenDispatchedEvent>().Should().ContainSingle()
            .Which.Children.Should().ContainSingle()
            .Which.ChildInstanceId.Should().Be(ChildId(2));
        plan.Events.OfType<WorkflowParentResumeTokenRecordedEvent>().Should().BeEmpty();
    }

    [Fact]
    public void PlanChildCompletion_WhenAnyCancelRemaining_RecordsResidualBeforeResume()
    {
        var group = ActiveGroup(
            joinPolicy: RunChildrenJoinPolicy.WhenAny,
            residualPolicy: RunChildrenResidualPolicy.CancelRemaining,
            children: [Child(0), Child(1)]);
        var state = DurableChildWorkflowState.FromSnapshot(
            [
                ActiveChild(0, group),
                ActiveChild(1, group)
            ],
            [group],
            [],
            [],
            [],
            []);

        var plan = state.PlanChildCompletion(
            ChildCompletionContext(CommandIdValue(10), Timestamp(10)),
            ChildCompleted(ChildId(0), WorkflowStatus.Completed));

        plan.Should().NotBeNull();
        plan!.Events.Select(workflowEvent => workflowEvent.GetType())
            .Should().ContainInOrder(
                typeof(WorkflowChildCompletedEvent),
                typeof(WorkflowChildResidualIntentRecordedEvent),
                typeof(WorkflowParentResumeTokenRecordedEvent),
                typeof(WorkflowWaitMatchedEvent));
    }

    [Fact]
    public void PlanResumeTokenConsumption_RequiresRecordedUnconsumedToken()
    {
        var token = EventIdValue(42);
        var state = DurableChildWorkflowState.FromSnapshot([], [], [], [], [token], []);

        var first = state.PlanResumeTokenConsumption(
            ChildCompletionContext(CommandIdValue(10), Timestamp(10)),
            GroupId(),
            token);
        state.Apply(first!);
        var second = state.PlanResumeTokenConsumption(
            ChildCompletionContext(CommandIdValue(11), Timestamp(11)),
            GroupId(),
            token);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public void PlanChildGroupCompensation_OrdersCompletedChildrenAndIsIdempotent()
    {
        var state = DurableChildWorkflowState.FromSnapshot(
            [],
            [],
            [
                new DurableCompletedChild(GroupId(), ChildId(2), "second", Timestamp(2)),
                new DurableCompletedChild(GroupId(), ChildId(1), "first", Timestamp(1))
            ],
            [],
            [],
            []);

        var first = state.PlanChildGroupCompensation(
            ChildCompletionContext(CommandIdValue(10), Timestamp(10)),
            GroupId(),
            DefinitionIdValue(9),
            DefinitionVersion.Initial);
        state.Apply(first!);
        var second = state.PlanChildGroupCompensation(
            ChildCompletionContext(CommandIdValue(11), Timestamp(11)),
            GroupId(),
            DefinitionIdValue(9),
            DefinitionVersion.Initial);

        first.Should().NotBeNull();
        first!.Compensations.Select(compensation => compensation.SourceChildInstanceId)
            .Should().Equal(ChildId(1), ChildId(2));
        second.Should().BeNull();
    }

    [Fact]
    public void ApplyResidualIntent_RemovesResidualChildrenAndReturnsWaitIdsToRemove()
    {
        var group = ActiveGroup(children: [Child(0), Child(1)]);
        var state = DurableChildWorkflowState.FromSnapshot(
            [
                ActiveChild(0, group),
                ActiveChild(1, group)
            ],
            [group],
            [],
            [],
            [],
            []);

        var effects = state.Apply(new WorkflowChildResidualIntentRecordedEvent
        {
            EventId = EventIdValue(20),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(20),
            CausationId = CausationIdValue(20),
            OccurredAt = Timestamp(20),
            GroupId = GroupId(),
            ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
            ResidualChildInstanceIds = [ChildId(1)]
        });

        effects.WaitIdsToRemove.Should().ContainSingle().Which.Should().Be(new WaitId(ChildId(1).Value));
        state.ActiveChildren.Should().ContainSingle().Which.ChildInstanceId.Should().Be(ChildId(0));
    }

    [Fact]
    public void CreateCheckpointActiveChildrenAndGroups_RoundTripsActiveState()
    {
        var group = ActiveGroup(children: [Child(0)]);
        var state = DurableChildWorkflowState.FromSnapshot([ActiveChild(0, group)], [group], [], [], [], []);

        var children = state.CreateCheckpointActiveChildren();
        var groups = state.CreateCheckpointActiveChildGroups();

        children.Should().ContainSingle().Which.ChildInstanceId.Should().Be(ChildId(0));
        groups.Should().ContainSingle().Which.GroupId.Should().Be(GroupId());
        children.Single().FiberId.Should().Be(new FiberId("child-fiber"));
        children.Single().ScopeId.Should().Be(new ScopeId("child-scope"));
        groups.Single().FiberId.Should().Be(new FiberId("child-fiber"));
        groups.Single().ScopeId.Should().Be(new ScopeId("child-scope"));
    }

    private static DurableActiveChildGroup ActiveGroup(
        int maxConcurrency = 2,
        int nextDispatchIndex = 1,
        RunChildrenJoinPolicy joinPolicy = RunChildrenJoinPolicy.WhenAll,
        RunChildrenResidualPolicy residualPolicy = RunChildrenResidualPolicy.CancelRemaining,
        IReadOnlyList<WorkflowChildMaterialization>? children = null)
    {
        return new DurableActiveChildGroup(
            GroupId(),
            RunChildFailurePolicy.PropagateFailure,
            joinPolicy,
            residualPolicy,
            maxConcurrency,
            nextDispatchIndex,
            children ?? [Child(0)])
        {
            FiberId = new FiberId("child-fiber"),
            ScopeId = new ScopeId("child-scope")
        };
    }

    private static DurableActiveChild ActiveChild(int index, DurableActiveChildGroup group)
    {
        return new DurableActiveChild(
            group.GroupId,
            ChildId(index),
            new WaitId(ChildId(index).Value),
            group.FailurePolicy,
            group.JoinPolicy,
            group.ResidualPolicy,
            Child(index).ItemSnapshot)
        {
            FiberId = group.FiberId,
            ScopeId = group.ScopeId
        };
    }

    private static WorkflowChildMaterialization Child(int index)
    {
        return new WorkflowChildMaterialization
        {
            Index = index,
            ChildInstanceId = ChildId(index),
            ChildDefinitionId = DefinitionIdValue(2),
            ChildDefinitionVersion = DefinitionVersion.Initial,
            ItemSnapshot = $"item-{index}"
        };
    }

    private static DurableChildCompletedCommand ChildCompleted(InstanceId childId, WorkflowStatus status)
    {
        return new DurableChildCompletedCommand(
            CommandIdValue(10),
            InstanceIdValue(1),
            Timestamp(10),
            childId,
            status,
            status == WorkflowStatus.Failed ? "boom" : null);
    }

    private static DurableChildWorkflowEventContext ChildCompletionContext(
        CommandId commandId,
        DateTimeOffset requestedAt)
    {
        return new DurableChildWorkflowEventContext(
            commandId,
            InstanceIdValue(1),
            requestedAt,
            ParentInstanceId: null,
            InstanceIdValue(1));
    }

    private static string GroupId()
    {
        return CommandIdValue(2).Value.ToString("D");
    }

    private static InstanceId ChildId(int index)
    {
        return InstanceIdValue(20 + index);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 15, 0, seconds, TimeSpan.Zero);
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
