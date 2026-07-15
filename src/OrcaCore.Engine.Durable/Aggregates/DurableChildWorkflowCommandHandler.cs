using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableChildWorkflowCommandHandler
{
    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, ConsumeParentResumeTokenCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.GroupId);
        var consumed = aggregate.ChildState.PlanResumeTokenConsumption(
            aggregate.CreateChildWorkflowEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.GroupId,
            command.ResumeTokenId);
        if (consumed is null)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        DurableLifecycleCommandHandler.AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            [],
            []);
        events.Add(consumed);

        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableRunChildCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var waitId = new WaitId(command.ChildInstanceId.Value);
        var events = new List<WorkflowEvent>();
        DurableLifecycleCommandHandler.AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            [],
            []);
        events.Add(
            new WorkflowChildScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ChildInstanceId = command.ChildInstanceId,
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                WaitId = waitId,
                FailurePolicy = command.FailurePolicy,
                FiberId = command.FiberId,
                ScopeId = command.ScopeId
            });

        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableChildCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var plan = aggregate.ChildState.PlanChildCompletion(
            aggregate.CreateChildWorkflowEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command);
        if (plan is null)
        {
            return DurableDecision.Empty;
        }

        if (plan.ShouldFailParent)
        {
            return new DurableDecision([
                .. plan.Events,
                .. aggregate.ChildState.CreateCancellationEvents(
                    aggregate.CreateChildWorkflowEventContext(
                        command.CommandId,
                        command.InstanceId,
                        command.RequestedAt),
                    excludedChildIds: new HashSet<InstanceId> { command.ChildInstanceId }),
                new WorkflowTerminalEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = aggregate.ParentInstanceId,
                    RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                    Status = WorkflowStatus.Failed
                }
            ]);
        }

        return new DurableDecision(plan.Events);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableRunChildrenCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var groupId = command.CommandId.Value.ToString("D");
        if (aggregate.ChildState.HasActiveChildInGroup(groupId))
        {
            return DurableDecision.Empty;
        }

        var children = command.ItemSnapshots
            .Select((itemSnapshot, index) => new WorkflowChildMaterialization
            {
                Index = index,
                ChildInstanceId = DurableChildWorkflowState.DeterministicChildId(command.InstanceId, command.CommandId, index),
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                ItemSnapshot = itemSnapshot
            })
            .ToArray();
        var maxConcurrency = Math.Min(command.MaxConcurrency ?? children.Length, children.Length);
        var initialDispatchCount = Math.Min(maxConcurrency, children.Length);
        var events = new List<WorkflowEvent>();
        DurableLifecycleCommandHandler.AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            [],
            []);
        events.Add(
            new WorkflowChildrenScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                GroupId = groupId,
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                FailurePolicy = command.FailurePolicy,
                JoinPolicy = command.JoinPolicy,
                ResidualPolicy = command.ResidualPolicy,
                TotalItemCount = children.Length,
                InitialDispatchCount = initialDispatchCount,
                NextDispatchIndex = initialDispatchCount,
                MaxConcurrency = maxConcurrency,
                Children = children,
                FiberId = command.FiberId,
                ScopeId = command.ScopeId
            });

        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CompensateChildGroupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var compensation = aggregate.ChildState.PlanChildGroupCompensation(
            aggregate.CreateChildWorkflowEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.GroupId,
            command.CompensationDefinitionId,
            command.CompensationDefinitionVersion);
        return compensation is null ? DurableDecision.Empty : new DurableDecision([compensation]);
    }
}
