using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.InMemory;

/// <summary>
/// Provides in-memory durable port implementations for tests and local execution.
/// </summary>
public sealed class InMemoryWorkflowProvider :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    IWorkflowRetentionStore,
    ITimerScheduler,
    IMessageDispatcher
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<EventId, InboxRecordState> inbox = [];
    private readonly Dictionary<string, StartedWorkflowIdempotencyRecord> startIdempotency = new(StringComparer.Ordinal);
    private readonly Dictionary<OutboxRecordId, InMemoryOutboxRecord> outbox = [];
    private readonly List<OutboxWrite> dispatched = [];
    private readonly List<ProjectionWrite> projections = [];
    private readonly List<ProjectionHistoryWrite> history = [];
    private readonly Dictionary<InstanceId, WorkflowInstanceSnapshot> summaries = [];
    private readonly Dictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly Dictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
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
            var stream = GetStream(batch.StreamId);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            stream.AddRange(batch.Events);
            ApplyInboxOperations(batch.InboxOperations);
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
    public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
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
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>(events);
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(inbox.TryGetValue(eventId, out var state)
                ? Option<InboxRecordState>.Some(state)
                : Option<InboxRecordState>.None);
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
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>(
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(summaries.Values.Count(snapshot => Matches(snapshot, query)));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ActiveWaitSnapshot>>(
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .SelectMany(snapshot => snapshot.ActiveWaits)
                    .Select(CloneActiveWait)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var groups = summaries.Values
                .Where(snapshot => Matches(snapshot, query))
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray();

            return Task.FromResult(new WorkflowStatistics
            {
                Groups = groups,
                Pressure = new WorkflowPressureMetrics
                {
                    TotalStreamEvents = streams.Values.Sum(stream => (long)stream.Count),
                    CheckpointCount = checkpoints.Count,
                    CheckpointLag = streams
                        .Select(stream =>
                        {
                            var checkpointVersion = checkpoints.TryGetValue(stream.Key.InstanceId, out var checkpoint)
                                ? checkpoint.StreamVersion.Value
                                : 0;
                            return Math.Max(0, stream.Value.Count - checkpointVersion);
                        })
                        .DefaultIfEmpty(0)
                        .Max(),
                    PendingOutboxCount = outbox.Values.Count(record =>
                        record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable),
                    OutboxPendingCount = outbox.Values.Count(record => record.State is OutboxRecordState.Pending),
                    OutboxRetryableCount = outbox.Values.Count(record => record.State is OutboxRecordState.Retryable),
                    OutboxClaimedCount = outbox.Values.Count(record => record.State is OutboxRecordState.Claimed),
                    ActiveInstanceCount = summaries.Values.Count(snapshot =>
                        snapshot.Status is WorkflowStatus.Running or WorkflowStatus.Waiting or WorkflowStatus.Paused)
                }
            });
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

    /// <inheritdoc />
    public Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (IsActive(policy.InstanceId))
            {
                return Task.FromResult(new ArchiveResult { Archived = false, Reason = "Instance is active." });
            }

            if (!summaries.TryGetValue(policy.InstanceId, out var snapshot))
            {
                return Task.FromResult(new ArchiveResult
                {
                    Archived = false,
                    Reason = "Instance projection was not found."
                });
            }

            summaries[policy.InstanceId] = snapshot with { ArchivedAt = policy.RequestedAt };
            return Task.FromResult(new ArchiveResult { Archived = true });
        }
    }

    /// <inheritdoc />
    public Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (IsActive(policy.InstanceId))
            {
                return Task.FromResult(new PurgeResult { Purged = false, Reason = "Instance is active." });
            }

            if (HasClaimedOutbox(policy.InstanceId))
            {
                return Task.FromResult(new PurgeResult
                {
                    Purged = false,
                    Reason = "Instance has claimed outbox records."
                });
            }

            DeleteInstanceData(policy.InstanceId);
            return Task.FromResult(new PurgeResult { Purged = true });
        }
    }

    private List<WorkflowEvent> GetStream(WorkflowStreamId streamId)
    {
        if (!streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            streams.Add(streamId, stream);
        }

        return stream;
    }

    private void ApplyInboxOperations(IEnumerable<InboxWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (inbox.TryGetValue(operation.EventId, out var existingState)
                && existingState == InboxRecordState.Applied)
            {
                continue;
            }

            inbox[operation.EventId] = operation.State;
        }
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
                    operation.DefinitionVersion));
        }
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

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            (query.ActiveWaitEventName is null || snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal))) &&
            (query.ActiveWaitCorrelationId is null || snapshot.ActiveWaits.Any(wait =>
                wait.CorrelationId == query.ActiveWaitCorrelationId));
    }

    private static WorkflowInstanceSnapshot CloneSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(CloneActiveWait).ToArray(),
            SagaAudits = snapshot.SagaAudits.Select(CloneSagaAuditScope).ToArray()
        };
    }

    private static ActiveWaitSnapshot CloneActiveWait(ActiveWaitSnapshot snapshot)
    {
        return snapshot with { };
    }

    private static SagaAuditScopeSnapshot CloneSagaAuditScope(SagaAuditScopeSnapshot snapshot)
    {
        return snapshot with
        {
            ForwardActions = snapshot.ForwardActions.Select(action => action with { }).ToArray(),
            CompensationActions = snapshot.CompensationActions.Select(action => action with { }).ToArray(),
            RecoveryInterventions = snapshot.RecoveryInterventions.Select(intervention => intervention with { }).ToArray()
        };
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
            BufferedDeliveries = state.BufferedDeliveries.Select(delivery => delivery with { }).ToArray(),
            BufferedTimers = state.BufferedTimers.Select(timer => timer with { }).ToArray(),
            ActiveChildren = state.ActiveChildren.Select(child => child with { }).ToArray(),
            ActiveChildGroups = state.ActiveChildGroups.Select(group => group with
            {
                Children = group.Children.Select(child => child with { }).ToArray()
            }).ToArray(),
            ActiveResourceTickets = state.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
            ActiveExternalJobs = state.ActiveExternalJobs.Select(job => job with { }).ToArray(),
            CompletedSagaForwardActions = state.CompletedSagaForwardActions.Select(action => action with { }).ToArray(),
            SagaCompensationActions = state.SagaCompensationActions.Select(action => action with { }).ToArray(),
            SagaRecoveryInterventions = state.SagaRecoveryInterventions.Select(intervention => intervention with { }).ToArray(),
            RequestedSagaCompensationScopes = [.. state.RequestedSagaCompensationScopes],
            RecordedParentResumeTokens = [.. state.RecordedParentResumeTokens],
            ConsumedParentResumeTokens = [.. state.ConsumedParentResumeTokens]
        };
    }

    private bool IsActive(InstanceId instanceId)
    {
        return summaries.TryGetValue(instanceId, out var snapshot) &&
            snapshot.Status is WorkflowStatus.Running or WorkflowStatus.Waiting or WorkflowStatus.Paused;
    }

    private bool HasClaimedOutbox(InstanceId instanceId)
    {
        return outbox.Values.Any(record =>
            record.InstanceId == instanceId &&
            record.State == OutboxRecordState.Claimed);
    }

    private void DeleteInstanceData(InstanceId instanceId)
    {
        summaries.Remove(instanceId);
        checkpoints.Remove(instanceId);
        streams.Remove(new WorkflowStreamId(instanceId));
        foreach (var timerId in timers.Values
            .Where(timer => timer.Request.InstanceId == instanceId)
            .Select(timer => timer.Request.TimerId)
            .ToArray())
        {
            timers.Remove(timerId);
        }

        var purgedHistoryIds = projections
            .Where(operation => operation.InstanceId == instanceId && operation.History is not null)
            .Select(operation => operation.History!.HistoryId)
            .ToHashSet();
        history.RemoveAll(entry => purgedHistoryIds.Contains(entry.HistoryId));
        foreach (var recordId in outbox.Values
            .Where(record => record.InstanceId == instanceId)
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
