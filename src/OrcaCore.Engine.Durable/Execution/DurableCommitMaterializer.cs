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

        var projectionOperations = aggregate.CreateProjectionWrites(decision.Events);
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            Events = decision.Events,
            Checkpoint = decision.Checkpoint,
            InboxOperations = CreateInboxOperations(inboxEventId, decision),
            OutboxRecords = CreateOutboxRecords(decision, aggregate, projectionOperations),
            ProjectionOperations = projectionOperations,
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

    private static IReadOnlyList<OutboxWrite> CreateOutboxRecords(
        DurableDecision decision,
        DurableWorkflowAggregate aggregate,
        IReadOnlyList<ProjectionWrite> projectionOperations)
    {
        var events = decision.Events;
        return CreateLifecycleOutboxRecords(events)
            .Concat(CreateChildStartOutboxRecords(events))
            .Concat(CreateResidualOutboxRecords(events))
            .Concat(CreateExternalJobOutboxRecords(events))
            .Concat(CreateContinuationOutboxRecords(decision, aggregate, projectionOperations))
            .ToArray();
    }

    /// <summary>
    /// DR-034: every commit that leaves the instance runnable carries an internal
    /// <c>continue</c> record inside the same commit boundary, so a host crash between commit
    /// and continuation never strands the instance. Runnability is judged from the committed
    /// facts: the projected status, an unblocking event in the batch (a wait matched or timer
    /// fired can leave the status Waiting while the unblocked branch is runnable), or a
    /// runnable cursor in the committed position envelope (a branch can suspend while a
    /// sibling still has work). Over-emission is harmless — a stale claim reloads the
    /// committed position and no-ops (DR-034 idempotence); under-emission strands.
    /// </summary>
    private static IReadOnlyList<OutboxWrite> CreateContinuationOutboxRecords(
        DurableDecision decision,
        DurableWorkflowAggregate aggregate,
        IReadOnlyList<ProjectionWrite> projectionOperations)
    {
        if (decision.Events.Count == 0)
        {
            return [];
        }

        var status = projectionOperations
            .Select(write => write.InstanceSnapshot?.Status)
            .FirstOrDefault(candidate => candidate is not null)
            ?? aggregate.Snapshot.Status;
        if (status is null
            or WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.Compensated
            or WorkflowStatus.CompensationFailed
            or WorkflowStatus.Paused
            or WorkflowStatus.Parked)
        {
            return [];
        }

        if (status != WorkflowStatus.Running
            && !decision.Events.Any(IsUnblockingEvent)
            && !HasRunnableEnvelopeCursor(decision.Checkpoint))
        {
            return [];
        }

        var lastEvent = decision.Events[^1];
        var failedAttempt = decision.Events.OfType<WorkflowContinuationAttemptFailedEvent>().LastOrDefault();
        var signal = new DurableContinuationSignal
        {
            InstanceId = lastEvent.InstanceId,
            OccurredAt = lastEvent.OccurredAt,
            NotBefore = failedAttempt?.NextEligibleAt
        };
        return [new OutboxWrite(OutboxRecordId.New(), OutboxKinds.Continue, signal.Serialize())];
    }

    private static bool IsUnblockingEvent(WorkflowEvent workflowEvent)
    {
        return workflowEvent
            is WorkflowWaitMatchedEvent
            or WorkflowTimerFiredEvent
            or WorkflowUnparkedEvent
            or WorkflowResumedEvent
            or WorkflowExternalJobCompletedEvent
            or WorkflowResourcePoolAcquiredEvent
            or WorkflowParentResumeTokenRecordedEvent;
    }

    private static bool HasRunnableEnvelopeCursor(CheckpointWrite? checkpoint)
    {
        if (checkpoint is null || checkpoint.ContentType != DurableExecutionEnvelope.ContentType)
        {
            return false;
        }

        try
        {
            var envelope = DurableExecutionEnvelope.Deserialize(checkpoint.Payload);
            return envelope.Position.Cursors.Any(cursor =>
                cursor.Phase is DurableCursorPhase.AtNode or DurableCursorPhase.Yielded);
        }
        catch (JsonException)
        {
            return false;
        }
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
            .Select(child => ChildStartOutboxRecord(
                "child-start",
                new CommandId(child.EventId.Value),
                child.ChildInstanceId,
                child.OccurredAt,
                child.InstanceId,
                child.RootInstanceId,
                child.ChildDefinitionId,
                child.ChildDefinitionVersion));
        var childGroups = events
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Take(group.InitialDispatchCount).Select(child =>
                ChildStartOutboxRecord(
                    "child-start",
                    new CommandId(child.ChildInstanceId.Value),
                    child.ChildInstanceId,
                    group.OccurredAt,
                    group.InstanceId,
                    group.RootInstanceId,
                    child.ChildDefinitionId,
                    child.ChildDefinitionVersion)));
        var dispatchedChildren = events
            .OfType<WorkflowChildrenDispatchedEvent>()
            .SelectMany(group => group.Children.Select(child => ChildStartOutboxRecord(
                "child-start",
                new CommandId(child.ChildInstanceId.Value),
                child.ChildInstanceId,
                group.OccurredAt,
                group.InstanceId,
                group.RootInstanceId,
                child.ChildDefinitionId,
                child.ChildDefinitionVersion)));
        var childCompensations = events
            .OfType<WorkflowChildCompensationScheduledEvent>()
            .SelectMany(group => group.Compensations.Select(compensation => ChildStartOutboxRecord(
                "child-compensation-start",
                new CommandId(compensation.CompensationInstanceId.Value),
                compensation.CompensationInstanceId,
                group.OccurredAt,
                group.InstanceId,
                group.RootInstanceId,
                group.CompensationDefinitionId,
                group.CompensationDefinitionVersion)));

        return [.. singleChildren, .. childGroups, .. dispatchedChildren, .. childCompensations];
    }

    /// <summary>
    /// Materializes one child-start outbox record: a serialized <see cref="StartWorkflowCommand"/>
    /// dispatching <paramref name="childInstanceId"/> under the parent's lineage.
    /// </summary>
    private static OutboxWrite ChildStartOutboxRecord(
        string kind,
        CommandId commandId,
        InstanceId childInstanceId,
        DateTimeOffset occurredAt,
        InstanceId parentInstanceId,
        InstanceId? rootInstanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return new OutboxWrite(
            OutboxRecordId.New(),
            kind,
            JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
            {
                CommandId = commandId,
                InstanceId = childInstanceId,
                RequestedAt = occurredAt,
                ParentInstanceId = parentInstanceId,
                RootInstanceId = rootInstanceId ?? parentInstanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion
            }));
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
            WorkflowParkedEvent parked =>
            [
                DurableLifecycleEvent(parked, "InstanceParked", null, WorkflowStatus.Parked)
            ],
            WorkflowUnparkedEvent unparked =>
            [
                DurableLifecycleEvent(unparked, "InstanceUnparked", null, WorkflowStatus.Running)
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
