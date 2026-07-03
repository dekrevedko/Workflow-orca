using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableCommitMaterializer
{
    internal ProviderCommitBatch CreateBatch(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        DurableDecision decision,
        DurableWorkflowAggregate aggregate,
        EventId? inboxEventId)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(aggregate);

        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            Events = decision.Events,
            Checkpoint = decision.Checkpoint,
            InboxOperations = CreateInboxOperations(inboxEventId, decision),
            OutboxRecords = CreateOutboxRecords(decision.Events),
            ProjectionOperations = aggregate.CreateProjectionWrites(decision.Events),
            StartIdempotencyOperations = CreateStartIdempotencyWrites(decision.Events),
            TimerSchedules = CreateTimerSchedules(decision.Events)
        };
    }

    internal ProviderCommitBatch CreateInboxOnlyBatch(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        EventId eventId,
        InboxRecordState state)
    {
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            InboxOperations = [new InboxWrite(eventId, state)]
        };
    }

    private static IReadOnlyList<StartIdempotencyWrite> CreateStartIdempotencyWrites(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .OfType<WorkflowStartedEvent>()
            .Where(started => !string.IsNullOrWhiteSpace(started.IdempotencyKey))
            .Select(started => new StartIdempotencyWrite(
                started.IdempotencyKey!,
                started.InstanceId,
                started.DefinitionId,
                started.DefinitionVersion))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return CreateLifecycleOutboxRecords(events)
            .Concat(CreateChildStartOutboxRecords(events))
            .Concat(CreateResidualOutboxRecords(events))
            .Concat(CreateExternalJobOutboxRecords(events))
            .ToArray();
    }

    private static IReadOnlyList<TimerScheduleRequest> CreateTimerSchedules(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .OfType<WorkflowTimerScheduledEvent>()
            .Select(timer => new TimerScheduleRequest
            {
                TimerId = timer.TimerId,
                InstanceId = timer.InstanceId,
                CommandId = new CommandId(timer.EventId.Value),
                FireAt = timer.FireAt,
                WakeupName = timer.WakeupName
            })
            .ToArray();
    }

    private static IReadOnlyList<InboxWrite> CreateInboxOperations(
        EventId? inboxEventId,
        DurableDecision decision)
    {
        return inboxEventId is { } eventId
            ? [new InboxWrite(eventId, InboundDeliveryState(decision)), .. decision.InboxOperations]
            : decision.InboxOperations;
    }

    private static InboxRecordState InboundDeliveryState(DurableDecision decision)
    {
        return decision.Events.Any(workflowEvent => workflowEvent is WorkflowDeliveryBufferedEvent)
            ? InboxRecordState.Received
            : InboxRecordState.Applied;
    }

    private static IReadOnlyList<OutboxWrite> CreateLifecycleOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .SelectMany(ToLifecycleEvents)
            .Select(lifecycleEvent => new OutboxWrite(
                OutboxRecordId.New(),
                "lifecycle-event",
                JsonSerializer.SerializeToUtf8Bytes(lifecycleEvent)))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateChildStartOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        var singleChildren = events
            .OfType<WorkflowChildScheduledEvent>()
            .Select(child => new OutboxWrite(
                OutboxRecordId.New(),
                "child-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(child.EventId.Value),
                    InstanceId = child.ChildInstanceId,
                    RequestedAt = child.OccurredAt,
                    ParentInstanceId = child.InstanceId,
                    RootInstanceId = child.RootInstanceId ?? child.InstanceId,
                    DefinitionId = child.ChildDefinitionId,
                    DefinitionVersion = child.ChildDefinitionVersion
                })))
            .ToArray();
        var childGroups = events
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Take(group.InitialDispatchCount).Select(child => new OutboxWrite(
                OutboxRecordId.New(),
                "child-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(child.ChildInstanceId.Value),
                    InstanceId = child.ChildInstanceId,
                    RequestedAt = group.OccurredAt,
                    ParentInstanceId = group.InstanceId,
                    RootInstanceId = group.RootInstanceId ?? group.InstanceId,
                    DefinitionId = child.ChildDefinitionId,
                    DefinitionVersion = child.ChildDefinitionVersion
                }))))
            .ToArray();
        var dispatchedChildren = events
            .OfType<WorkflowChildrenDispatchedEvent>()
            .SelectMany(group => group.Children.Select(child => new OutboxWrite(
                OutboxRecordId.New(),
                "child-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(child.ChildInstanceId.Value),
                    InstanceId = child.ChildInstanceId,
                    RequestedAt = group.OccurredAt,
                    ParentInstanceId = group.InstanceId,
                    RootInstanceId = group.RootInstanceId ?? group.InstanceId,
                    DefinitionId = child.ChildDefinitionId,
                    DefinitionVersion = child.ChildDefinitionVersion
                }))))
            .ToArray();
        var childCompensations = events
            .OfType<WorkflowChildCompensationScheduledEvent>()
            .SelectMany(group => group.Compensations.Select(compensation => new OutboxWrite(
                OutboxRecordId.New(),
                "child-compensation-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(compensation.CompensationInstanceId.Value),
                    InstanceId = compensation.CompensationInstanceId,
                    RequestedAt = group.OccurredAt,
                    ParentInstanceId = group.InstanceId,
                    RootInstanceId = group.RootInstanceId ?? group.InstanceId,
                    DefinitionId = group.CompensationDefinitionId,
                    DefinitionVersion = group.CompensationDefinitionVersion
                }))))
            .ToArray();

        return [.. singleChildren, .. childGroups, .. dispatchedChildren, .. childCompensations];
    }

    private static IReadOnlyList<OutboxWrite> CreateResidualOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .OfType<WorkflowChildResidualIntentRecordedEvent>()
            .SelectMany(residual => residual.ResidualChildInstanceIds.Select(childId => new OutboxWrite(
                OutboxRecordId.New(),
                "child-cancel",
                JsonSerializer.SerializeToUtf8Bytes(new CancelWorkflowCommand
                {
                    CommandId = new CommandId(childId.Value),
                    InstanceId = childId,
                    RequestedAt = residual.OccurredAt,
                    ParentInstanceId = residual.InstanceId,
                    RootInstanceId = residual.RootInstanceId ?? residual.InstanceId
                }))))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateExternalJobOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        var starts = events
            .OfType<WorkflowExternalJobStartedEvent>()
            .Select(started => new OutboxWrite(
                OutboxRecordId.New(),
                "external-job-start",
                JsonSerializer.SerializeToUtf8Bytes(started)))
            .ToArray();
        var stops = events
            .OfType<WorkflowExternalJobStopRequestedEvent>()
            .Select(stop => new OutboxWrite(
                OutboxRecordId.New(),
                "external-job-stop",
                JsonSerializer.SerializeToUtf8Bytes(stop)))
            .ToArray();

        return [.. starts, .. stops];
    }

    private static IEnumerable<LifecycleEventSnapshot> ToLifecycleEvents(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent started =>
            [
                DurableLifecycleEvent(started, "InstanceStarted", null, WorkflowStatus.Running)
            ],
            WorkflowStepCompletedEvent stepCompleted =>
            [
                DurableLifecycleEvent(stepCompleted, "StepCompleted", stepCompleted.StepPath, WorkflowStatus.Running)
            ],
            WorkflowStepFailedEvent stepFailed =>
            [
                DurableLifecycleEvent(stepFailed, "StepFailed", stepFailed.StepPath, WorkflowStatus.Failed)
            ],
            WorkflowWaitRegisteredEvent waitRegistered =>
            [
                DurableLifecycleEvent(waitRegistered, "InstanceSuspended", null, WorkflowStatus.Waiting)
            ],
            WorkflowWaitMatchedEvent waitMatched =>
            [
                DurableLifecycleEvent(waitMatched, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowTimerScheduledEvent timerScheduled =>
            [
                DurableLifecycleEvent(timerScheduled, "InstanceSuspended", null, WorkflowStatus.Waiting)
            ],
            WorkflowTimerFiredEvent timerFired =>
            [
                DurableLifecycleEvent(timerFired, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowPausedEvent paused =>
            [
                DurableLifecycleEvent(paused, "InstancePaused", null, WorkflowStatus.Paused)
            ],
            WorkflowResumedEvent resumed =>
            [
                DurableLifecycleEvent(resumed, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowCompletedEvent completed =>
            [
                DurableLifecycleEvent(completed, "InstanceCompleted", null, WorkflowStatus.Completed)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Failed } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceFailed", null, WorkflowStatus.Failed)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCancelled", null, WorkflowStatus.Cancelled)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Terminated } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceTerminated", null, WorkflowStatus.Terminated)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Compensated } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCompensated", null, WorkflowStatus.Compensated)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.CompensationFailed } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCompensationFailed", null, WorkflowStatus.CompensationFailed)
            ],
            _ => []
        };
    }

    private static LifecycleEventSnapshot DurableLifecycleEvent(
        WorkflowEvent workflowEvent,
        string eventName,
        string? stepPath,
        WorkflowStatus status)
    {
        return new LifecycleEventSnapshot
        {
            InstanceId = workflowEvent.InstanceId,
            EventName = eventName,
            StepPath = stepPath,
            Status = status,
            OccurredAt = workflowEvent.OccurredAt,
            Durable = true
        };
    }
}
