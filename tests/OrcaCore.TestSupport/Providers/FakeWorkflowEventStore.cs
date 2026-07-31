using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using ProjectionActiveWaitSnapshot = global::OrcaCore.Abstractions.Instances.ActiveWaitSnapshot;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Instances.WorkflowInstanceSnapshot;

namespace OrcaCore.TestSupport.Providers;

public sealed class FakeWorkflowEventStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly object gate = new();
    private readonly ConcurrentDictionary<(InstanceId InstanceId, EventId EventId), InboxRecord> inbox = [];
    private readonly ConcurrentDictionary<string, StartedWorkflowIdempotencyRecord> startIdempotency =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<OutboxRecordId, FakeOutboxRecord> outbox = [];
    private readonly ConcurrentDictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly ConcurrentDictionary<WorkflowStreamId, List<DurableWorkflowEvent>> streams = [];
    private readonly ConcurrentDictionary<InstanceId, ProjectionWorkflowInstanceSnapshot> projectedSummaries = [];
    private readonly List<ProviderCommitBatch> committedBatches = [];
    private int failNextCommitBeforeApply;

    public IReadOnlyList<ProviderCommitBatch> CommittedBatches
    {
        get
        {
            lock (gate)
            {
                return committedBatches.ToArray();
            }
        }
    }

    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(checkpoints.TryGetValue(instanceId, out var checkpoint)
            ? Option<CheckpointWrite>.Some(CloneCheckpointWrite(checkpoint))
            : Option<CheckpointWrite>.None);
    }

    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (Interlocked.Exchange(ref failNextCommitBeforeApply, 0) == 1)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    batch.ExpectedVersion));
            }

            var stream = streams.GetOrAdd(batch.StreamId, _ => []);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            if (HasStartIdempotencyConflict(batch.StartIdempotencyOperations))
            {
                return Task.FromResult(EventStoreConflict.StartIdempotencyKeyAlreadyExists(
                    batch.StartIdempotencyOperations[0].IdempotencyKey));
            }

            stream.AddRange(batch.Events);
            foreach (var operation in batch.InboxOperations)
            {
                var key = (batch.StreamId.InstanceId, operation.EventId);
                if (inbox.TryGetValue(key, out var existing))
                {
                    inbox[key] = existing with
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
                        $"Initial inbox write '{operation.EventId}' for instance " +
                        $"'{batch.StreamId.InstanceId}' must carry an envelope fingerprint.");
                }

                inbox[key] = new InboxRecord(
                    batch.StreamId.InstanceId,
                    operation.EventId,
                    operation.EnvelopeFingerprint,
                    operation.State);
            }

            foreach (var operation in batch.StartIdempotencyOperations)
            {
                startIdempotency[operation.IdempotencyKey] = new StartedWorkflowIdempotencyRecord(
                    operation.IdempotencyKey,
                    operation.InstanceId,
                    operation.DefinitionId,
                    operation.DefinitionVersion,
                    operation.DefinitionFingerprint,
                    operation.InputFingerprint);
            }

            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new FakeOutboxRecord(
                    record with { Payload = [.. record.Payload] },
                    OutboxRecordState.Pending);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpointWrite(checkpoint);
            }

            committedBatches.Add(batch);

            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(new StreamVersion(stream.Count))));
        }
    }

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

    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(inbox.TryGetValue((instanceId, eventId), out var record)
            ? Option<InboxRecord>.Some(record)
            : Option<InboxRecord>.None);
    }

    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(startIdempotency.TryGetValue(idempotencyKey, out var record)
            ? Option<StartedWorkflowIdempotencyRecord>.Some(record)
            : Option<StartedWorkflowIdempotencyRecord>.None);
    }

    private bool HasStartIdempotencyConflict(IReadOnlyList<StartIdempotencyWrite> operations)
    {
        return operations
            .Select(operation => operation.IdempotencyKey)
            .GroupBy(key => key, StringComparer.Ordinal)
            .Any(group => group.Count() > 1 || startIdempotency.ContainsKey(group.Key));
    }

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        return ClaimAsync(
            new OutboxClaimRequest(maxCount, DateTimeOffset.UtcNow, DefaultLeaseDuration),
            cancellationToken);
    }

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
                .Select(record => record.Write with { Payload = [.. record.Write.Payload] })
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

    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
            ? Option<OutboxRecordState>.Some(record.State)
            : Option<OutboxRecordState>.None);
    }

    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outbox.TryGetValue(outboxRecordId, out var record))
        {
            outbox[outboxRecordId] = record with
            {
                State = state,
                ClaimedUntil = state == OutboxRecordState.Claimed ? record.ClaimedUntil : null
            };
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outbox.TryGetValue(outboxRecordId, out var record) &&
            record.State == OutboxRecordState.Claimed)
        {
            outbox[outboxRecordId] = record with
            {
                State = OutboxRecordState.Retryable,
                ClaimedUntil = null
            };
        }

        return Task.CompletedTask;
    }

    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var operation in operations)
        {
            if (operation is { Kind: ProjectionOperationKind.UpsertSummary, InstanceSnapshot: { } snapshot })
            {
                projectedSummaries[operation.InstanceId] = snapshot;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>>(
            projectedSummaries.Values
                .Where(snapshot => Matches(snapshot, query))
                .OrderBy(snapshot => snapshot.InstanceId.Value)
                .ToArray());
    }

    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(projectedSummaries.Values.Count(snapshot => Matches(snapshot, query)));
    }

    public Task<IReadOnlyList<ProjectionActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionActiveWaitSnapshot>>(
            projectedSummaries.Values
                .Where(snapshot => Matches(snapshot, query))
                .SelectMany(snapshot => snapshot.ActiveWaits)
                .ToArray());
    }

    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = projectedSummaries.Values.Where(snapshot => Matches(snapshot, query)).ToArray();
        return Task.FromResult(new WorkflowStatistics
        {
            Groups = matches
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
                .ToArray(),
            Pressure = new WorkflowPressureMetrics
            {
                PendingOutboxCount = outbox.Values.Count(record =>
                    record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable),
                OutboxPendingCount = outbox.Values.Count(record => record.State is OutboxRecordState.Pending),
                OutboxRetryableCount = outbox.Values.Count(record => record.State is OutboxRecordState.Retryable),
                OutboxClaimedCount = outbox.Values.Count(record => record.State is OutboxRecordState.Claimed),
                ContinuationPendingCount = outbox.Values.Count(record =>
                    record.Write.Kind == OutboxKinds.Continue && record.State is OutboxRecordState.Pending),
                ContinuationRetryableCount = outbox.Values.Count(record =>
                    record.Write.Kind == OutboxKinds.Continue && record.State is OutboxRecordState.Retryable),
                ContinuationClaimedCount = outbox.Values.Count(record =>
                    record.Write.Kind == OutboxKinds.Continue && record.State is OutboxRecordState.Claimed),
                ExternalOutboxPendingCount = outbox.Values.Count(record =>
                    record.Write.Kind != OutboxKinds.Continue && record.State is OutboxRecordState.Pending),
                ExternalOutboxRetryableCount = outbox.Values.Count(record =>
                    record.Write.Kind != OutboxKinds.Continue && record.State is OutboxRecordState.Retryable),
                ExternalOutboxClaimedCount = outbox.Values.Count(record =>
                    record.Write.Kind != OutboxKinds.Continue && record.State is OutboxRecordState.Claimed),
                ActiveInstanceCount = projectedSummaries.Values.Count(snapshot =>
                    snapshot.Status is WorkflowStatus.Running or WorkflowStatus.Waiting or WorkflowStatus.Paused)
            }
        });
    }

    private static bool Matches(
        ProjectionWorkflowInstanceSnapshot snapshot,
        WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            MatchesActiveWait(snapshot.ActiveWaits, query);
    }

    private static bool MatchesActiveWait(
        IReadOnlyList<ProjectionActiveWaitSnapshot> activeWaits,
        WorkflowProjectionQuery query)
    {
        if (query.ActiveWaitEventName is null && query.ActiveWaitCorrelationId is null)
        {
            return true;
        }

        return activeWaits.Any(wait =>
            (query.ActiveWaitEventName is null ||
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal)) &&
            (query.ActiveWaitCorrelationId is null ||
                wait.CorrelationId.Equals(query.ActiveWaitCorrelationId)));
    }

    public void FailNextCommitBeforeApply()
    {
        Interlocked.Exchange(ref failNextCommitBeforeApply, 1);
    }

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with
        {
            Payload = [.. checkpoint.Payload],
            RuntimeState = new WorkflowRuntimeCheckpointState
            {
                ActiveTimers = checkpoint.RuntimeState.ActiveTimers.Select(timer => timer with { }).ToArray(),
                ActiveWaits = checkpoint.RuntimeState.ActiveWaits.Select(wait => wait with { }).ToArray(),
                BufferedDeliveries = checkpoint.RuntimeState.BufferedDeliveries.Select(delivery => delivery with { }).ToArray(),
                BufferedTimers = checkpoint.RuntimeState.BufferedTimers.Select(timer => timer with { }).ToArray(),
                ActiveChildren = checkpoint.RuntimeState.ActiveChildren.Select(child => child with { }).ToArray(),
                ActiveChildGroups = checkpoint.RuntimeState.ActiveChildGroups.Select(group => group with
                {
                    Children = group.Children.Select(child => child with { }).ToArray()
                }).ToArray(),
                ActiveResourceTickets = checkpoint.RuntimeState.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
                ActiveExternalJobs = checkpoint.RuntimeState.ActiveExternalJobs.Select(job => job with { }).ToArray(),
                CompletedSagaForwardActions = checkpoint.RuntimeState.CompletedSagaForwardActions.Select(action => action with { }).ToArray(),
                SagaCompensationActions = checkpoint.RuntimeState.SagaCompensationActions.Select(action => action with { }).ToArray(),
                SagaRecoveryInterventions = checkpoint.RuntimeState.SagaRecoveryInterventions.Select(intervention => intervention with { }).ToArray(),
                RequestedSagaCompensationScopes = [.. checkpoint.RuntimeState.RequestedSagaCompensationScopes],
                RecordedParentResumeTokens = [.. checkpoint.RuntimeState.RecordedParentResumeTokens],
                ConsumedParentResumeTokens = [.. checkpoint.RuntimeState.ConsumedParentResumeTokens]
            }
        };
    }

    private static bool IsOutboxClaimable(FakeOutboxRecord record, DateTimeOffset claimedAt)
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

    private sealed record FakeOutboxRecord(
        OutboxWrite Write,
        OutboxRecordState State,
        DateTimeOffset? ClaimedUntil = null);
}
