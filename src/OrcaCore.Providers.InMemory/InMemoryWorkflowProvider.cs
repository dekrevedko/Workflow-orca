using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Providers.InMemory;

/// <summary>
/// Provides in-memory durable port implementations for tests and local execution.
/// </summary>
internal sealed class InMemoryWorkflowProvider :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IMessageDispatcher
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<EventId, InboxRecord> inbox = [];
    private readonly Dictionary<string, StartedWorkflowIdempotencyRecord> startIdempotency = new(StringComparer.Ordinal);
    private readonly Dictionary<OutboxRecordId, InMemoryOutboxRecord> outbox = [];
    private readonly List<OutboxWrite> dispatched = [];
    private readonly List<ProjectionWrite> projections = [];
    private readonly List<ProjectionHistoryWrite> history = [];
    private readonly Dictionary<InstanceId, WorkflowProjectionSnapshot> summaries = [];
    private readonly Dictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly Dictionary<WorkflowStreamId, List<DurableWorkflowEvent>> streams = [];
    private readonly Dictionary<TimerId, InMemoryTimerSchedule> timers = [];
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes the in-memory workflow provider.
    /// </summary>
    public InMemoryWorkflowProvider(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(checkpoints.TryGetValue(instanceId, out var checkpoint)
                ? Option<CheckpointWrite>.Some(CloneCheckpointWrite(checkpoint))
                : Option<CheckpointWrite>.None);
        }
    }

    /// <inheritdoc />
    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var conflictingEventId = batch.InboxOperations
                .Where(operation => operation.EnvelopeFingerprint is not null)
                .Where(operation => inbox.TryGetValue(operation.EventId, out var existing) &&
                    (!existing.InstanceId.Equals(batch.StreamId.InstanceId) ||
                     !string.Equals(
                         existing.EnvelopeFingerprint,
                         operation.EnvelopeFingerprint,
                         StringComparison.Ordinal)))
                .Select(operation => operation.EventId)
                .FirstOrDefault();
            if (conflictingEventId is not null)
            {
                return Task.FromResult(EventStoreConflict.EventIdAlreadyExists(conflictingEventId));
            }

            var stream = GetStream(batch.StreamId);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            var conflictingStartKey = FindConflictingStartIdempotencyKey(batch.StartIdempotencyOperations);
            if (conflictingStartKey is not null)
            {
                return Task.FromResult(EventStoreConflict.StartIdempotencyKeyAlreadyExists(conflictingStartKey));
            }
            stream.AddRange(batch.Events);
            ApplyInboxOperations(batch.StreamId.InstanceId, batch.InboxOperations);
            ApplyStartIdempotencyOperations(batch.StartIdempotencyOperations);
            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new InMemoryOutboxRecord(
                    batch.StreamId.InstanceId,
                    CloneOutboxWrite(record),
                    OutboxRecordState.Pending);
            }

            ApplyProjectionOperations(batch.ProjectionOperations);
            foreach (var timer in batch.TimerSchedules)
            {
                timers[timer.TimerId] = new InMemoryTimerSchedule(timer, ClaimedUntil: null);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpointWrite(checkpoint);
            }

            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(new StreamVersion(stream.Count))));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var events = streams.TryGetValue(streamId, out var stream)
                ? stream.Skip((int)afterVersion.Value).ToArray()
                : [];
            return Task.FromResult<IReadOnlyList<DurableWorkflowEvent>>(events);
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(inbox.TryGetValue(eventId, out var record) && record.InstanceId.Equals(instanceId)
                ? Option<InboxRecord>.Some(record)
                : Option<InboxRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(inbox.TryGetValue(eventId, out var record)
                ? Option<InboxRecord>.Some(record)
                : Option<InboxRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(startIdempotency.TryGetValue(idempotencyKey, out var record)
                ? Option<StartedWorkflowIdempotencyRecord>.Some(record)
                : Option<StartedWorkflowIdempotencyRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        return ClaimAsync(
            new OutboxClaimRequest(maxCount, timeProvider.GetUtcNow(), DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var claimed = outbox.Values
                .Where(record => IsOutboxClaimable(record, request.ClaimedAt))
                .Where(record => request.KindSelector is null || request.KindSelector.Matches(record.Write.Kind))
                .Take(request.MaxCount)
                .Select(record => CloneOutboxWrite(record.Write))
                .ToArray();
            foreach (var record in claimed)
            {
                outbox[record.OutboxRecordId] = outbox[record.OutboxRecordId] with
                {
                    State = OutboxRecordState.Claimed,
                    ClaimedUntil = request.ClaimedAt.Add(request.LeaseDuration)
                };
            }

            return Task.FromResult<IReadOnlyList<OutboxWrite>>(claimed);
        }
    }

    /// <inheritdoc />
    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
                ? Option<OutboxRecordState>.Some(record.State)
                : Option<OutboxRecordState>.None);
        }
    }

    /// <inheritdoc />
    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (outbox.TryGetValue(outboxRecordId, out var record))
            {
                outbox[outboxRecordId] = record with
                {
                    State = state,
                    ClaimedUntil = state == OutboxRecordState.Claimed ? record.ClaimedUntil : null
                };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (outbox.TryGetValue(outboxRecordId, out var record) &&
                record.State == OutboxRecordState.Claimed)
            {
                outbox[outboxRecordId] = record with
                {
                    State = OutboxRecordState.Retryable,
                    ClaimedUntil = null
                };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            ApplyProjectionOperations(operations);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(summaries.TryGetValue(instanceId, out var snapshot)
                ? Option<WorkflowProjectionSnapshot>.Some(CloneSnapshot(snapshot))
                : Option<WorkflowProjectionSnapshot>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values
                    .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                    .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                        string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                        wait.CorrelationId.Equals(correlationId)))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(eventContractVersion);
        ArgumentNullException.ThrowIfNull(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values
                    .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                    .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                        string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                        wait.EventContractVersion == eventContractVersion.Value &&
                        wait.CorrelationId.Equals(correlationId)))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values.Select(CloneSnapshot).ToArray());
        }
    }

    /// <inheritdoc />
    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            timers[request.TimerId] = new InMemoryTimerSchedule(request, ClaimedUntil: null);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var due = timers.Values
                .Where(timer => timer.Request.FireAt <= request.DueAtOrBefore &&
                    (timer.ClaimedUntil is null || timer.ClaimedUntil <= request.ClaimedAt))
                .OrderBy(timer => timer.Request.FireAt)
                .ThenBy(timer => timer.Request.TimerId.Value)
                .Take(request.MaxCount)
                .ToArray();
            foreach (var timer in due)
            {
                timers[timer.Request.TimerId] = timer with
                {
                    ClaimedUntil = request.ClaimedAt.Add(request.LeaseDuration)
                };
            }

            return Task.FromResult<IReadOnlyList<FireTimerCommand>>(due.Select(timer => new FireTimerCommand
            {
                CommandId = timer.Request.CommandId,
                InstanceId = timer.Request.InstanceId,
                RequestedAt = request.ClaimedAt,
                TimerId = timer.Request.TimerId
            }).ToArray());
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            timers.Remove(timerId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (timers.TryGetValue(timerId, out var timer))
            {
                timers[timerId] = timer with { ClaimedUntil = null };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            dispatched.Add(CloneOutboxWrite(record));
        }

        return Task.FromResult(DispatchResult.Success);
    }

    internal Task<(bool Purged, string? Reason)> PurgeForRetentionAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (IsActive(instanceId))
            {
                return Task.FromResult((false, (string?)"Instance is active."));
            }

            if (HasClaimedOutbox(instanceId))
            {
                return Task.FromResult((false, (string?)"Instance has claimed outbox records."));
            }

            DeleteInstanceData(instanceId);
            return Task.FromResult((true, (string?)null));
        }
    }

    private List<DurableWorkflowEvent> GetStream(WorkflowStreamId streamId)
    {
        if (!streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            streams.Add(streamId, stream);
        }

        return stream;
    }

    private void ApplyInboxOperations(InstanceId instanceId, IEnumerable<InboxWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (inbox.TryGetValue(operation.EventId, out var existing))
            {
                inbox[operation.EventId] = existing with
                {
                    State = existing.State == InboxRecordState.Applied
                        ? existing.State
                        : operation.State
                };
                continue;
            }

            if (string.IsNullOrWhiteSpace(operation.EnvelopeFingerprint))
            {
                throw new InvalidOperationException(
                    $"Initial inbox write '{operation.EventId}' for instance '{instanceId}' " +
                    "must carry an envelope fingerprint.");
            }

            inbox[operation.EventId] = new InboxRecord(
                instanceId,
                operation.EventId,
                operation.EnvelopeFingerprint,
                operation.State)
            {
                Envelope = CloneEnvelope(operation.Envelope)
            };
        }
    }

    private static DurableEventEnvelope? CloneEnvelope(DurableEventEnvelope? envelope)
    {
        return envelope is null
            ? null
            : envelope with
            {
                Payload = envelope.Payload is null ? null : [.. envelope.Payload],
                Route = envelope.Route with
                {
                    WorkflowInputPayload = envelope.Route.WorkflowInputPayload is null
                        ? null
                        : [.. envelope.Route.WorkflowInputPayload]
                }
            };
    }

    private void ApplyStartIdempotencyOperations(IEnumerable<StartIdempotencyWrite> operations)
    {
        foreach (var operation in operations)
        {
            startIdempotency.TryAdd(
                operation.IdempotencyKey,
                new StartedWorkflowIdempotencyRecord(
                    operation.IdempotencyKey,
                     operation.InstanceId,
                     operation.DefinitionId,
                     operation.DefinitionVersion,
                     operation.DefinitionFingerprint,
                     operation.InputFingerprint));
        }
    }

    private string? FindConflictingStartIdempotencyKey(IReadOnlyList<StartIdempotencyWrite> operations)
    {
        return operations
            .Select(operation => operation.IdempotencyKey)
            .GroupBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1 || startIdempotency.ContainsKey(group.Key))
            ?.Key;
    }

    private void ApplyProjectionOperations(IEnumerable<ProjectionWrite> operations)
    {
        foreach (var operation in operations)
        {
            projections.Add(operation);
            if (operation.InstanceSnapshot is { } snapshot)
            {
                summaries[operation.InstanceId] = CloneSnapshot(snapshot);
            }

            if (operation.Kind == ProjectionOperationKind.AppendHistory && operation.History is { } entry)
            {
                history.Add(entry);
            }
        }
    }

    private static WorkflowProjectionSnapshot CloneSnapshot(WorkflowProjectionSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(CloneActiveWait).ToArray()
        };
    }

    private static WorkflowProjectionActiveWaitSnapshot CloneActiveWait(
        WorkflowProjectionActiveWaitSnapshot snapshot)
    {
        return snapshot with { };
    }

    private static OutboxWrite CloneOutboxWrite(OutboxWrite record)
    {
        return record with { Payload = [.. record.Payload] };
    }

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with
        {
            Payload = [.. checkpoint.Payload],
            RuntimeState = CloneRuntimeState(checkpoint.RuntimeState)
        };
    }

    private static WorkflowRuntimeCheckpointState CloneRuntimeState(WorkflowRuntimeCheckpointState state)
    {
        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers = state.ActiveTimers.Select(timer => timer with { }).ToArray(),
            ActiveWaits = state.ActiveWaits.Select(wait => wait with { }).ToArray(),
            ActiveResourceTickets = state.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
            PendingResumes = state.PendingResumes.Select(pending => pending with
            {
                Payload = pending.Payload?.ToArray()
            }).ToArray(),
            ContinuationFailureCount = state.ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = state.ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = state.ContinuationRetryNotBefore
        };
    }

    private bool IsActive(InstanceId instanceId)
    {
        return summaries.TryGetValue(instanceId, out var snapshot) && snapshot.Status is
            global::OrcaCore.WorkflowInstanceStatus.Pending or
            global::OrcaCore.WorkflowInstanceStatus.Running or
            global::OrcaCore.WorkflowInstanceStatus.Waiting or
            global::OrcaCore.WorkflowInstanceStatus.CancellationRequested;
    }

    private bool HasClaimedOutbox(InstanceId instanceId)
    {
        return outbox.Values.Any(record =>
            record.InstanceId.Equals(instanceId) &&
            record.State == OutboxRecordState.Claimed);
    }

    private void DeleteInstanceData(InstanceId instanceId)
    {
        summaries.Remove(instanceId);
        checkpoints.Remove(instanceId);
        streams.Remove(new WorkflowStreamId(instanceId));
        foreach (var timerId in timers.Values
            .Where(timer => timer.Request.InstanceId.Equals(instanceId))
            .Select(timer => timer.Request.TimerId)
            .ToArray())
        {
            timers.Remove(timerId);
        }

        var purgedHistoryIds = projections
            .Where(operation => operation.InstanceId.Equals(instanceId) && operation.History is not null)
            .Select(operation => operation.History!.HistoryId)
            .ToHashSet();
        history.RemoveAll(entry => purgedHistoryIds.Contains(entry.HistoryId));
        foreach (var recordId in outbox.Values
            .Where(record => record.InstanceId.Equals(instanceId))
            .Select(record => record.Write.OutboxRecordId)
            .ToArray())
        {
            outbox.Remove(recordId);
        }
    }

    private static bool IsOutboxClaimable(InMemoryOutboxRecord record, DateTimeOffset claimedAt)
    {
        return record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable ||
            (record.State == OutboxRecordState.Claimed &&
                (record.ClaimedUntil is null || record.ClaimedUntil <= claimedAt));
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }

    private sealed record InMemoryOutboxRecord(
        InstanceId InstanceId,
        OutboxWrite Write,
        OutboxRecordState State,
        DateTimeOffset? ClaimedUntil = null);

    private sealed record InMemoryTimerSchedule(TimerScheduleRequest Request, DateTimeOffset? ClaimedUntil);
}
