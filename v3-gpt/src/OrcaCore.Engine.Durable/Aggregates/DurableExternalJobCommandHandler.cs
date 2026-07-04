using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableExternalJobCommandHandler
{
    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        RunExternalJobCommand command,
        ResourcePoolAcquireResult? acquireResult)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.ExternalJobState.Find(command.ExternalJobId) is not null)
        {
            return DurableDecision.Empty;
        }

        if (acquireResult is { Status: ResourcePoolAcquireStatus.Rejected })
        {
            return DurableDecision.Empty;
        }

        var resourcePoolAcquirePlan = acquireResult is null
            ? DurableResourcePoolAcquirePlan.Empty
            : aggregate.ResourcePoolState.PlanAcquire(
                aggregate.CreateResourcePoolEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
                command.ExternalJobId,
                command.Requirements,
                command.TimeoutAt,
                acquireResult);
        if (resourcePoolAcquirePlan.EvictAfterCommit)
        {
            return new DurableDecision(resourcePoolAcquirePlan.Events, null, true);
        }

        var waitId = WaitId.New();
        TimerId? timeoutTimerId = command.TimeoutAt is null ? null : TimerId.New();
        var events = new List<WorkflowEvent>(resourcePoolAcquirePlan.Events);

        events.Add(new WorkflowExternalJobStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            ExternalJobId = command.ExternalJobId,
            Payload = [.. command.Payload],
            WaitId = waitId,
            TimeoutTimerId = timeoutTimerId,
            TimeoutAt = command.TimeoutAt
        });
        events.Add(new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            WaitId = waitId,
            EventName = "ExternalJobCompleted",
            CorrelationId = new CorrelationId(command.ExternalJobId),
            Mode = WaitMode.Cold
        });

        if (timeoutTimerId is { } timerId && command.TimeoutAt is { } timeoutAt)
        {
            events.Add(new WorkflowTimerScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                TimerId = timerId,
                FireAt = timeoutAt,
                WakeupName = $"ExternalJobTimeout:{command.ExternalJobId}"
            });
        }

        return new DurableDecision(events, null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CompleteExternalJobCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var job = aggregate.ExternalJobState.Find(command.ExternalJobId);
        if (aggregate.IsTerminal || job is null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowExternalJobCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ExternalJobId = command.ExternalJobId,
                CompletionEventId = command.CompletionEventId
            },
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt, command.ExternalJobId),
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                WaitId = job.WaitId,
                MatchedEventId = command.CompletionEventId
            }
        ]);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, TimeoutExternalJobCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var job = aggregate.ExternalJobState.Find(command.ExternalJobId);
        if (aggregate.IsTerminal || job is null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowExternalJobTimedOutEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ExternalJobId = command.ExternalJobId
            },
            new WorkflowExternalJobStopRequestedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ExternalJobId = command.ExternalJobId
            },
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt, command.ExternalJobId),
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
        ], null, true);
    }
}
