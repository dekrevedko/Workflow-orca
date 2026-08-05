using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Execution;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

internal sealed class DurableCommitMaterializer
{
    internal ProviderCommitBatch CreateBatch(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        DurableDecision decision,
        DurableWorkflowAggregate aggregate,
        DurableInboxDelivery? inboxDelivery)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(aggregate);

        var projectionOperations = aggregate.CreateProjectionWrites(decision.Events, decision.Checkpoint);
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            Events = decision.Events,
            Checkpoint = decision.Checkpoint,
            InboxOperations = CreateInboxOperations(inboxDelivery, decision),
            OutboxRecords = CreateOutboxRecords(decision, aggregate, projectionOperations),
            ProjectionOperations = projectionOperations,
            StartIdempotencyOperations = CreateStartIdempotencyWrites(decision.Events),
            TimerSchedules = CreateTimerSchedules(decision.Events)
        };
    }

    internal ProviderCommitBatch CreateInboxOnlyBatch(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        DurableInboxDelivery delivery,
        InboxRecordState state)
    {
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            InboxOperations =
            [
                new InboxWrite(delivery.EventId, state)
                {
                    EnvelopeFingerprint = delivery.EnvelopeFingerprint
                }
            ]
        };
    }

    private static IReadOnlyList<StartIdempotencyWrite> CreateStartIdempotencyWrites(IReadOnlyList<DurableWorkflowEvent> events)
    {
        return events
            .OfType<WorkflowStartedEvent>()
            .Where(started => !string.IsNullOrWhiteSpace(started.IdempotencyKey))
            .Select(started => new StartIdempotencyWrite(
                started.IdempotencyKey!,
                started.InstanceId,
                started.DefinitionId,
                started.DefinitionVersion,
                started.DefinitionFingerprint ?? throw new InvalidOperationException(
                    "An idempotent start must carry its definition fingerprint."),
                started.InputFingerprint ?? throw new InvalidOperationException(
                    "An idempotent start must carry its fixed-codec input fingerprint.")))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateOutboxRecords(
        DurableDecision decision,
        DurableWorkflowAggregate aggregate,
        IReadOnlyList<ProjectionWrite> projectionOperations)
    {
        return CreateContinuationOutboxRecords(decision, aggregate, projectionOperations);
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
        if (decision.Events.Count == 0 && !HasRunnableEnvelopeWork(decision.Checkpoint))
        {
            return [];
        }

        var status = projectionOperations
            .Select(write => write.InstanceSnapshot?.Status)
            .FirstOrDefault(candidate => candidate is not null)
            ?? aggregate.Snapshot.Status;
        var parkTransition = decision.Events.LastOrDefault(workflowEvent =>
            workflowEvent is WorkflowParkedEvent or WorkflowUnparkedEvent);
        var isParked = parkTransition is WorkflowParkedEvent ||
            (parkTransition is null && aggregate.ParkReason is not null);
        if (status is null
            or WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.TimedOut ||
            isParked)
        {
            return [];
        }

        if (status is not (WorkflowStatus.Running or WorkflowStatus.CancellationRequested)
            && !decision.Events.Any(IsUnblockingEvent)
            && !HasRunnableEnvelopeWork(decision.Checkpoint))
        {
            return [];
        }

        var lastEvent = decision.Events.LastOrDefault();
        var failedAttempt = decision.Events.OfType<WorkflowContinuationAttemptFailedEvent>().LastOrDefault();
        var signal = new DurableContinuationSignal
        {
            InstanceId = lastEvent?.InstanceId ?? decision.Checkpoint!.InstanceId,
            OccurredAt = lastEvent?.OccurredAt ?? aggregate.UpdatedAt ?? DateTimeOffset.UnixEpoch,
            NotBefore = failedAttempt?.NextEligibleAt
        };
        return [new OutboxWrite(OutboxRecordId.New(), OutboxKinds.Continue, signal.Serialize())];
    }

    private static bool IsUnblockingEvent(DurableWorkflowEvent workflowEvent)
    {
        return workflowEvent
            is WorkflowWaitMatchedEvent
            or WorkflowTimerFiredEvent
            or WorkflowUnparkedEvent
            or WorkflowResourcePoolAcquiredEvent;
    }

    private static bool HasRunnableEnvelopeWork(CheckpointWrite? checkpoint)
    {
        if (checkpoint is null)
        {
            return false;
        }

        try
        {
            if (checkpoint.ContentType == DurableExecutionEnvelopeV2.ContentType)
            {
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
                var runnable = envelope.Fibers
                    .Where(fiber => fiber.Phase == DurableFiberPhase.Runnable)
                    .Select(fiber => fiber.FiberId)
                    .ToHashSet(StringComparer.Ordinal);
                return envelope.Scheduler.NextFiberId is { } next && runnable.Contains(next);
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<TimerScheduleRequest> CreateTimerSchedules(IReadOnlyList<DurableWorkflowEvent> events)
    {
        return events
            .OfType<WorkflowTimerScheduledEvent>()
            .Select(timer => new TimerScheduleRequest
            {
                TimerId = timer.TimerId,
                InstanceId = timer.InstanceId,
                CommandId = new CommandId(timer.TimerId.Value),
                FireAt = timer.FireAt,
                WakeupName = timer.WakeupName
            })
            .ToArray();
    }

    private static IReadOnlyList<InboxWrite> CreateInboxOperations(
        DurableInboxDelivery? inboxDelivery,
        DurableDecision decision)
    {
        return inboxDelivery is { } delivery
            ?
            [
                new InboxWrite(delivery.EventId, InboxRecordState.Applied)
                {
                    EnvelopeFingerprint = delivery.EnvelopeFingerprint
                },
                .. decision.InboxOperations
            ]
            : decision.InboxOperations;
    }

}
