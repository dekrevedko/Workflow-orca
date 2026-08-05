using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;

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

    public Task<Option<ProjectionWorkflowInstanceSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(projectedSummaries.TryGetValue(instanceId, out var snapshot)
            ? Option<ProjectionWorkflowInstanceSnapshot>.Some(snapshot)
            : Option<ProjectionWorkflowInstanceSnapshot>.None);
    }

    public Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>>(
            projectedSummaries.Values
                .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                    string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                    wait.CorrelationId.Equals(correlationId)))
                .OrderBy(snapshot => snapshot.InstanceId.Value)
                .ToArray());
    }

    public Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>>(
            projectedSummaries.Values
                .OrderBy(snapshot => snapshot.InstanceId.Value)
                .ToArray());
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
                ActiveResourceTickets = checkpoint.RuntimeState.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
                PendingResumes = checkpoint.RuntimeState.PendingResumes.Select(resume => resume with
                {
                    Payload = resume.Payload is null ? null : [.. resume.Payload]
                }).ToArray(),
                ContinuationFailureCount = checkpoint.RuntimeState.ContinuationFailureCount,
                ContinuationFailurePositionStreamVersion = checkpoint.RuntimeState.ContinuationFailurePositionStreamVersion,
                ContinuationRetryNotBefore = checkpoint.RuntimeState.ContinuationRetryNotBefore
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
