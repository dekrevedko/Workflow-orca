using System.Security.Cryptography;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableChildWorkflowState
{
    private readonly List<DurableActiveChild> activeChildren;
    private readonly List<DurableActiveChildGroup> activeChildGroups;
    private readonly List<DurableCompletedChild> completedChildren;
    private readonly HashSet<string> compensatedChildGroups;
    private readonly HashSet<EventId> recordedParentResumeTokens;
    private readonly HashSet<EventId> consumedParentResumeTokens;

    private DurableChildWorkflowState(
        IEnumerable<DurableActiveChild> activeChildren,
        IEnumerable<DurableActiveChildGroup> activeChildGroups,
        IEnumerable<DurableCompletedChild> completedChildren,
        IEnumerable<string> compensatedChildGroups,
        IEnumerable<EventId> recordedParentResumeTokens,
        IEnumerable<EventId> consumedParentResumeTokens)
    {
        this.activeChildren = [.. activeChildren];
        this.activeChildGroups = [.. activeChildGroups];
        this.completedChildren = [.. completedChildren];
        this.compensatedChildGroups = new HashSet<string>(compensatedChildGroups, StringComparer.Ordinal);
        this.recordedParentResumeTokens = [.. recordedParentResumeTokens];
        this.consumedParentResumeTokens = [.. consumedParentResumeTokens];
    }

    internal IReadOnlyList<DurableActiveChild> ActiveChildren => [.. activeChildren];

    internal IReadOnlyList<DurableActiveChildGroup> ActiveChildGroups => [.. activeChildGroups];

    internal IReadOnlyList<EventId> RecordedParentResumeTokens => [.. recordedParentResumeTokens];

    internal IReadOnlyList<EventId> ConsumedParentResumeTokens => [.. consumedParentResumeTokens];

    internal static DurableChildWorkflowState FromSnapshot(
        IEnumerable<DurableActiveChild> activeChildren,
        IEnumerable<DurableActiveChildGroup> activeChildGroups,
        IEnumerable<DurableCompletedChild> completedChildren,
        IEnumerable<string> compensatedChildGroups,
        IEnumerable<EventId> recordedParentResumeTokens,
        IEnumerable<EventId> consumedParentResumeTokens)
    {
        ArgumentNullException.ThrowIfNull(activeChildren);
        ArgumentNullException.ThrowIfNull(activeChildGroups);
        ArgumentNullException.ThrowIfNull(completedChildren);
        ArgumentNullException.ThrowIfNull(compensatedChildGroups);
        ArgumentNullException.ThrowIfNull(recordedParentResumeTokens);
        ArgumentNullException.ThrowIfNull(consumedParentResumeTokens);

        return new DurableChildWorkflowState(
            activeChildren,
            activeChildGroups,
            completedChildren,
            compensatedChildGroups,
            recordedParentResumeTokens,
            consumedParentResumeTokens);
    }

    internal bool HasActiveChildInGroup(string groupId)
    {
        return activeChildren.Any(child => string.Equals(child.GroupId, groupId, StringComparison.Ordinal));
    }

    internal IReadOnlyList<WorkflowChildResidualIntentRecordedEvent> CreateCancellationEvents(
        DurableChildWorkflowEventContext context,
        IReadOnlySet<FiberId>? ownerFiberIds = null,
        IReadOnlySet<InstanceId>? excludedChildIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return activeChildren
            .Where(child => ownerFiberIds is null ||
                child.FiberId is { } fiberId && ownerFiberIds.Contains(fiberId))
            .Where(child => excludedChildIds is null || !excludedChildIds.Contains(child.ChildInstanceId))
            .GroupBy(child => child.GroupId, StringComparer.Ordinal)
            .Select(group => new WorkflowChildResidualIntentRecordedEvent
            {
                EventId = EventId.New(),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                ParentInstanceId = context.ParentInstanceId,
                RootInstanceId = context.RootInstanceId,
                GroupId = group.Key,
                ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
                ResidualChildInstanceIds = group
                    .Select(child => child.ChildInstanceId)
                    .OrderBy(childId => childId.Value)
                    .ToArray()
            })
            .ToArray();
    }

    internal void ClearActiveChildren()
    {
        activeChildren.Clear();
    }

    internal DurableChildCompletionPlan? PlanChildCompletion(
        DurableChildWorkflowEventContext context,
        DurableChildCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var activeChild = activeChildren.FirstOrDefault(child => child.ChildInstanceId == command.ChildInstanceId);
        if (activeChild is null)
        {
            return null;
        }

        var childCompleted = new WorkflowChildCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            ChildInstanceId = command.ChildInstanceId,
            ChildStatus = command.ChildStatus,
            ErrorSummary = command.ErrorSummary
        };
        var waitMatched = new WorkflowWaitMatchedEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            WaitId = activeChild.WaitId,
            MatchedEventId = childCompleted.EventId
        };
        var childDispatch = DispatchChildrenIfCapacity(context, activeChild);
        var residualIntent = ResidualIntentIfNeeded(context, activeChild);
        var resumeToken = ResumeTokenIfGroupComplete(context, activeChild, childDispatch);
        var shouldFailParent = command.ChildStatus == WorkflowStatus.Failed &&
            activeChild.FailurePolicy is RunChildFailurePolicy.PropagateFailure;

        var events = new List<WorkflowEvent> { childCompleted };
        if (!shouldFailParent && childDispatch is not null)
        {
            events.Add(childDispatch);
        }

        events.AddRange(residualIntent);
        events.AddRange(resumeToken);
        events.Add(waitMatched);

        return new DurableChildCompletionPlan(events, shouldFailParent);
    }

    internal WorkflowChildCompensationScheduledEvent? PlanChildGroupCompensation(
        DurableChildWorkflowEventContext context,
        string groupId,
        DefinitionId compensationDefinitionId,
        DefinitionVersion compensationDefinitionVersion)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        if (compensatedChildGroups.Contains(groupId))
        {
            return null;
        }

        var eligibleChildren = completedChildren
            .Where(child => string.Equals(child.GroupId, groupId, StringComparison.Ordinal))
            .OrderBy(child => child.CompletedAt)
            .ThenBy(child => child.ChildInstanceId.Value)
            .ToArray();
        if (eligibleChildren.Length == 0)
        {
            return null;
        }

        return new WorkflowChildCompensationScheduledEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            GroupId = groupId,
            CompensationDefinitionId = compensationDefinitionId,
            CompensationDefinitionVersion = compensationDefinitionVersion,
            Compensations = eligibleChildren
                .Select((child, index) => new WorkflowChildCompensationMaterialization
                {
                    Index = index,
                    SourceChildInstanceId = child.ChildInstanceId,
                    CompensationInstanceId = DeterministicChildId(context.InstanceId, context.CommandId, index),
                    ItemSnapshot = child.ItemSnapshot ?? string.Empty
                })
                .ToArray()
        };
    }

    internal WorkflowParentResumeTokenConsumedEvent? PlanResumeTokenConsumption(
        DurableChildWorkflowEventContext context,
        string groupId,
        EventId resumeTokenId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);

        if (!recordedParentResumeTokens.Contains(resumeTokenId) ||
            consumedParentResumeTokens.Contains(resumeTokenId))
        {
            return null;
        }

        return new WorkflowParentResumeTokenConsumedEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            GroupId = groupId,
            ResumeTokenId = resumeTokenId
        };
    }

    internal DurableChildReplayEffects Apply(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        switch (workflowEvent)
        {
            case WorkflowChildScheduledEvent childScheduled:
                var singleChild = new DurableActiveChild(
                    childScheduled.ChildInstanceId.Value.ToString("D"),
                    childScheduled.ChildInstanceId,
                    childScheduled.WaitId,
                    childScheduled.FailurePolicy,
                    RunChildrenJoinPolicy.WhenAll,
                    RunChildrenResidualPolicy.CancelRemaining,
                    null)
                {
                    FiberId = childScheduled.FiberId,
                    ScopeId = childScheduled.ScopeId
                };
                activeChildren.Add(singleChild);
                return new DurableChildReplayEffects([WaitForChild(singleChild, childScheduled.OccurredAt)], [], null);
            case WorkflowChildrenScheduledEvent childrenScheduled:
                activeChildGroups.RemoveAll(group =>
                    string.Equals(group.GroupId, childrenScheduled.GroupId, StringComparison.Ordinal));
                activeChildGroups.Add(new DurableActiveChildGroup(
                    childrenScheduled.GroupId,
                    childrenScheduled.FailurePolicy,
                    childrenScheduled.JoinPolicy,
                    childrenScheduled.ResidualPolicy,
                    childrenScheduled.MaxConcurrency,
                    childrenScheduled.NextDispatchIndex,
                    childrenScheduled.Children)
                {
                    FiberId = childrenScheduled.FiberId,
                    ScopeId = childrenScheduled.ScopeId
                });
                return ActivateChildren(
                    childrenScheduled.GroupId,
                    childrenScheduled.FailurePolicy,
                    childrenScheduled.JoinPolicy,
                    childrenScheduled.ResidualPolicy,
                    childrenScheduled.Children.Take(childrenScheduled.InitialDispatchCount),
                    childrenScheduled.OccurredAt,
                    childrenScheduled.FiberId,
                    childrenScheduled.ScopeId);
            case WorkflowChildrenDispatchedEvent childrenDispatched:
                var activeGroup = activeChildGroups.FirstOrDefault(group =>
                    string.Equals(group.GroupId, childrenDispatched.GroupId, StringComparison.Ordinal));
                if (activeGroup is null)
                {
                    return DurableChildReplayEffects.Empty;
                }

                activeChildGroups.Remove(activeGroup);
                activeChildGroups.Add(activeGroup with { NextDispatchIndex = childrenDispatched.NextDispatchIndex });
                return ActivateChildren(
                    activeGroup.GroupId,
                    activeGroup.FailurePolicy,
                    activeGroup.JoinPolicy,
                    activeGroup.ResidualPolicy,
                    childrenDispatched.Children,
                    childrenDispatched.OccurredAt,
                    activeGroup.FiberId,
                    activeGroup.ScopeId);
            case WorkflowChildCompletedEvent childCompleted:
                return ApplyChildCompleted(childCompleted);
            case WorkflowChildCompensationScheduledEvent childCompensationScheduled:
                compensatedChildGroups.Add(childCompensationScheduled.GroupId);
                return DurableChildReplayEffects.Empty;
            case WorkflowChildResidualIntentRecordedEvent residualIntent:
                return ApplyResidualIntent(residualIntent);
            case WorkflowParentResumeTokenRecordedEvent parentResumeToken:
                recordedParentResumeTokens.Add(parentResumeToken.ResumeTokenId);
                activeChildGroups.RemoveAll(group =>
                    string.Equals(group.GroupId, parentResumeToken.GroupId, StringComparison.Ordinal));
                return DurableChildReplayEffects.Empty;
            case WorkflowParentResumeTokenConsumedEvent parentResumeTokenConsumed:
                consumedParentResumeTokens.Add(parentResumeTokenConsumed.ResumeTokenId);
                return DurableChildReplayEffects.Empty;
            default:
                return DurableChildReplayEffects.Empty;
        }
    }

    internal IReadOnlyList<CheckpointActiveChild> CreateCheckpointActiveChildren()
    {
        return activeChildren
            .Select(child => new CheckpointActiveChild(
                child.GroupId,
                child.ChildInstanceId,
                child.WaitId,
                child.FailurePolicy,
                child.JoinPolicy,
                child.ResidualPolicy,
                child.ItemSnapshot)
            {
                FiberId = child.FiberId,
                ScopeId = child.ScopeId
            })
            .ToArray();
    }

    internal IReadOnlyList<CheckpointActiveChildGroup> CreateCheckpointActiveChildGroups()
    {
        return activeChildGroups
            .Select(group => new CheckpointActiveChildGroup(
                group.GroupId,
                group.FailurePolicy,
                group.JoinPolicy,
                group.ResidualPolicy,
                group.MaxConcurrency,
                group.NextDispatchIndex,
                group.Children)
            {
                FiberId = group.FiberId,
                ScopeId = group.ScopeId
            })
            .ToArray();
    }

    internal static InstanceId DeterministicChildId(InstanceId parentInstanceId, CommandId commandId, int index)
    {
        Span<byte> input = stackalloc byte[36];
        parentInstanceId.Value.TryWriteBytes(input[..16]);
        commandId.Value.TryWriteBytes(input.Slice(16, 16));
        BitConverter.TryWriteBytes(input.Slice(32, 4), index);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new InstanceId(new Guid(hash[..16]));
    }

    private WorkflowChildrenDispatchedEvent? DispatchChildrenIfCapacity(
        DurableChildWorkflowEventContext context,
        DurableActiveChild activeChild)
    {
        if (activeChild.JoinPolicy is not RunChildrenJoinPolicy.WhenAll)
        {
            return null;
        }

        var group = activeChildGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.GroupId, activeChild.GroupId, StringComparison.Ordinal));
        if (group is null || group.NextDispatchIndex >= group.Children.Count)
        {
            return null;
        }

        var activeAfterCompletion = activeChildren.Count(child =>
            string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
            child.ChildInstanceId != activeChild.ChildInstanceId);
        var availableSlots = group.MaxConcurrency - activeAfterCompletion;
        if (availableSlots <= 0)
        {
            return null;
        }

        var children = group.Children
            .Skip(group.NextDispatchIndex)
            .Take(availableSlots)
            .ToArray();
        if (children.Length == 0)
        {
            return null;
        }

        return new WorkflowChildrenDispatchedEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            GroupId = activeChild.GroupId,
            PreviousDispatchIndex = group.NextDispatchIndex,
            NextDispatchIndex = group.NextDispatchIndex + children.Length,
            Children = children
        };
    }

    private IReadOnlyList<WorkflowChildResidualIntentRecordedEvent> ResidualIntentIfNeeded(
        DurableChildWorkflowEventContext context,
        DurableActiveChild activeChild)
    {
        if (activeChild.JoinPolicy is not RunChildrenJoinPolicy.WhenAny ||
            activeChild.ResidualPolicy is not RunChildrenResidualPolicy.CancelRemaining)
        {
            return [];
        }

        var residualChildIds = activeChildren
            .Where(child => string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
                child.ChildInstanceId != activeChild.ChildInstanceId)
            .Select(child => child.ChildInstanceId)
            .ToArray();
        return residualChildIds.Length == 0
            ? []
            :
            [
                new WorkflowChildResidualIntentRecordedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = context.InstanceId,
                    CommandId = context.CommandId,
                    CausationId = context.CausationId,
                    OccurredAt = context.RequestedAt,
                    ParentInstanceId = context.ParentInstanceId,
                    RootInstanceId = context.RootInstanceId,
                    GroupId = activeChild.GroupId,
                    ResidualPolicy = activeChild.ResidualPolicy,
                    ResidualChildInstanceIds = residualChildIds
                }
            ];
    }

    private IReadOnlyList<WorkflowParentResumeTokenRecordedEvent> ResumeTokenIfGroupComplete(
        DurableChildWorkflowEventContext context,
        DurableActiveChild activeChild,
        WorkflowChildrenDispatchedEvent? childDispatch)
    {
        if (activeChild.JoinPolicy is RunChildrenJoinPolicy.WhenAny)
        {
            return [ResumeToken(context, activeChild)];
        }

        var remainingInGroup = activeChildren.Count(child =>
            string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
            child.ChildInstanceId != activeChild.ChildInstanceId) +
            (childDispatch?.Children.Count ?? 0);
        var group = activeChildGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.GroupId, activeChild.GroupId, StringComparison.Ordinal));
        var nextDispatchIndex = childDispatch?.NextDispatchIndex ?? group?.NextDispatchIndex ?? 0;
        if (remainingInGroup > 0 || (group is not null && nextDispatchIndex < group.Children.Count))
        {
            return [];
        }

        return [ResumeToken(context, activeChild)];
    }

    private WorkflowParentResumeTokenRecordedEvent ResumeToken(
        DurableChildWorkflowEventContext context,
        DurableActiveChild activeChild)
    {
        return new WorkflowParentResumeTokenRecordedEvent
        {
            EventId = EventId.New(),
            InstanceId = context.InstanceId,
            CommandId = context.CommandId,
            CausationId = context.CausationId,
            OccurredAt = context.RequestedAt,
            ParentInstanceId = context.ParentInstanceId,
            RootInstanceId = context.RootInstanceId,
            GroupId = activeChild.GroupId,
            ResumeTokenId = new EventId(Guid.Parse(activeChild.GroupId))
        };
    }

    private DurableChildReplayEffects ActivateChildren(
        string groupId,
        RunChildFailurePolicy failurePolicy,
        RunChildrenJoinPolicy joinPolicy,
        RunChildrenResidualPolicy residualPolicy,
        IEnumerable<WorkflowChildMaterialization> children,
        DateTimeOffset occurredAt,
        FiberId? fiberId,
        ScopeId? scopeId)
    {
        var waits = new List<DurableActiveWait>();
        foreach (var child in children)
        {
            var activeChild = new DurableActiveChild(
                groupId,
                child.ChildInstanceId,
                new WaitId(child.ChildInstanceId.Value),
                failurePolicy,
                joinPolicy,
                residualPolicy,
                child.ItemSnapshot)
            {
                FiberId = fiberId,
                ScopeId = scopeId
            };
            activeChildren.Add(activeChild);
            waits.Add(WaitForChild(activeChild, occurredAt));
        }

        return new DurableChildReplayEffects(waits, [], null);
    }

    private DurableChildReplayEffects ApplyChildCompleted(WorkflowChildCompletedEvent childCompleted)
    {
        var completedChild = activeChildren.FirstOrDefault(
            child => child.ChildInstanceId == childCompleted.ChildInstanceId);
        if (completedChild is not null && childCompleted.ChildStatus is WorkflowStatus.Completed)
        {
            completedChildren.RemoveAll(child =>
                string.Equals(child.GroupId, completedChild.GroupId, StringComparison.Ordinal) &&
                child.ChildInstanceId == childCompleted.ChildInstanceId);
            completedChildren.Add(new DurableCompletedChild(
                completedChild.GroupId,
                childCompleted.ChildInstanceId,
                completedChild.ItemSnapshot,
                childCompleted.OccurredAt));
        }

        activeChildren.RemoveAll(child => child.ChildInstanceId == childCompleted.ChildInstanceId);
        var errorSummary = childCompleted.ChildStatus == WorkflowStatus.Failed &&
            completedChild?.FailurePolicy is RunChildFailurePolicy.PropagateFailure
            ? childCompleted.ErrorSummary
            : null;
        return new DurableChildReplayEffects([], [], errorSummary);
    }

    private DurableChildReplayEffects ApplyResidualIntent(WorkflowChildResidualIntentRecordedEvent residualIntent)
    {
        var waitIdsToRemove = new List<WaitId>();
        foreach (var residualChildId in residualIntent.ResidualChildInstanceIds)
        {
            var residualChild = activeChildren.FirstOrDefault(
                child => child.ChildInstanceId == residualChildId);
            if (residualChild is not null)
            {
                waitIdsToRemove.Add(residualChild.WaitId);
            }

            activeChildren.RemoveAll(child => child.ChildInstanceId == residualChildId);
        }

        if (!activeChildren.Any(child =>
                string.Equals(child.GroupId, residualIntent.GroupId, StringComparison.Ordinal)))
        {
            activeChildGroups.RemoveAll(group =>
                string.Equals(group.GroupId, residualIntent.GroupId, StringComparison.Ordinal));
        }

        return new DurableChildReplayEffects([], waitIdsToRemove, null);
    }

    private static DurableActiveWait WaitForChild(DurableActiveChild child, DateTimeOffset registeredAt)
    {
        return new DurableActiveWait(
            child.WaitId,
            "ChildCompleted",
            ChildCorrelation(child.ChildInstanceId),
            registeredAt)
        {
            FiberId = child.FiberId,
            ScopeId = child.ScopeId
        };
    }

    private static CorrelationId ChildCorrelation(InstanceId childInstanceId)
    {
        return new CorrelationId(childInstanceId.Value.ToString("D"));
    }
}

internal sealed record DurableChildCompletionPlan(
    IReadOnlyList<WorkflowEvent> Events,
    bool ShouldFailParent);

internal sealed record DurableChildReplayEffects(
    IReadOnlyList<DurableActiveWait> WaitsToRegister,
    IReadOnlyList<WaitId> WaitIdsToRemove,
    string? PropagatedFailureErrorSummary)
{
    internal static DurableChildReplayEffects Empty { get; } = new([], [], null);
}

internal sealed record DurableChildWorkflowEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId? ParentInstanceId,
    InstanceId RootInstanceId)
{
    internal CausationId CausationId => new(CommandId.Value);
}
