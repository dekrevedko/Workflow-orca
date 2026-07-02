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
    private readonly object gate = new();
    private readonly ConcurrentDictionary<EventId, InboxRecordState> inbox = [];
    private readonly ConcurrentDictionary<OutboxRecordId, FakeOutboxRecord> outbox = [];
    private readonly ConcurrentDictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly ConcurrentDictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
    private int failNextCommitBeforeApply;

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
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IReadOnlyList<OutboxWrite>>(
            outbox.Values
                .Where(record => record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable)
                .Take(maxCount)
                .Select(record => record.Write with { Payload = [.. record.Write.Payload] })
                .ToArray());
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
            outbox[outboxRecordId] = record with { State = state };
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
        return checkpoint with { Payload = [.. checkpoint.Payload] };
    }

    private sealed record FakeOutboxRecord(OutboxWrite Write, OutboxRecordState State);
}
