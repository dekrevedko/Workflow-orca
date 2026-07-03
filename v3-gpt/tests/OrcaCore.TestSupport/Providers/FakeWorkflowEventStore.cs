using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.TestSupport.Providers;

public sealed class FakeWorkflowEventStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly object gate = new();
    private readonly ConcurrentDictionary<EventId, InboxRecordState> inbox = [];
    private readonly ConcurrentDictionary<OutboxRecordId, FakeOutboxRecord> outbox = [];
    private readonly ConcurrentDictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly ConcurrentDictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
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

            stream.AddRange(batch.Events);
            foreach (var operation in batch.InboxOperations)
            {
                inbox[operation.EventId] = operation.State;
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

    public Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(inbox.TryGetValue(eventId, out var state)
            ? Option<InboxRecordState>.Some(state)
            : Option<InboxRecordState>.None);
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
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>([]);
    }

    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }

    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ActiveWaitSnapshot>>([]);
    }

    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new WorkflowStatistics { Groups = [] });
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
                ActiveExternalJobs = checkpoint.RuntimeState.ActiveExternalJobs.Select(job => job with { }).ToArray()
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
