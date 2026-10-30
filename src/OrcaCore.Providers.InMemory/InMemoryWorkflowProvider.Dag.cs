using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Providers.Dag.Internal;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Providers.InMemory;

internal sealed partial class InMemoryWorkflowProvider
{
    private readonly Dictionary<Guid, DagRunStoredState> dagRuns = [];
    private long dagCreationSequence;
    private long dagLeaseSequence;

    private Task<Result<AppendEventsResult>> AppendDagRunAsync(ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        try
        {
            DagRunStoreRules.ValidateBatch(batch);
        }
        catch (InvalidDataException exception)
        {
            return Task.FromResult(DagRunStoreRules.Failure("DAG_PROTOCOL_INVALID", exception.Message));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var id = batch.StreamId.DagRunId!.Value;
            if (streams.Keys.Any(stream =>
                    stream.AggregateKind == DurableAggregateKind.Workflow && stream.InstanceId.Value == id))
                return Task.FromResult(DagRunStoreRules.Failure("DAG_INTEGRITY_CONFLICT",
                    "workflow owns this aggregate identity"));
            dagRuns.TryGetValue(id, out var previous);
            if (previous is null)
            {
                var creation = batch.DagRun!.Records[0].CreationSnapshot;
                if (creation is null)
                    return Task.FromResult(DagRunStoreRules.Failure("DAG_PROTOCOL_INVALID", "missing creation"));
                if (dagRuns.Values.Any(run =>
                        string.Equals(run.Snapshot.StartKey.Value, creation.StartKey.Value, StringComparison.Ordinal)))
                    return Task.FromResult(DagRunStoreRules.Failure("DAG_START_KEY_CONFLICT",
                        "caller key is already bound"));
            }

            var candidate = previous is null ? null : DagRunStoreRules.CopyForWrite(previous);
            var result = DagRunStoreRules.Append(candidate, batch, timeProvider.GetUtcNow(),
                previous?.CreationSequence ?? checked(dagCreationSequence + 1), out var updated);
            if (result.IsFailure || updated is null) return Task.FromResult(result);
            foreach (var write in updated.NewOutbox)
                if (outbox.TryGetValue(write.OutboxRecordId, out var existing) &&
                    (existing.Write.Kind != write.Kind || existing.Write.DagRunOwner != write.DagRunOwner ||
                     existing.Write.RecipientHostId != write.RecipientHostId ||
                     existing.Write.DagRunSubscriptionId != write.DagRunSubscriptionId ||
                     !existing.Write.Payload.AsSpan().SequenceEqual(write.Payload)))
                    return Task.FromResult(DagRunStoreRules.Failure("DAG_INTEGRITY_CONFLICT",
                        "outbox identity collision"));
            foreach (var write in updated.NewOutbox)
                outbox.TryAdd(write.OutboxRecordId, new InMemoryOutboxRecord(InstanceId.Parse(id.ToString("D")),
                    DagRunStoreRules.Clone(write), OutboxRecordState.Pending));
            updated.NewOutbox.Clear();
            dagRuns[id] = updated;
            if (previous is null) dagCreationSequence = updated.CreationSequence;
            return Task.FromResult(result);
        }
    }

    public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(WorkflowStreamId streamId,
        CancellationToken cancellationToken)
    {
        var id = DagRunStoreRules.RequireDagStream(streamId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!dagRuns.TryGetValue(id, out var run)) return Task.FromResult(Option<DagRunCheckpointV1>.None);
            _ = DagRunStoreRules.ReadCheckpoint(run.Checkpoint);
            return Task.FromResult(Option<DagRunCheckpointV1>.Some(DagRunStoreRules.Clone(run.Checkpoint)));
        }
    }

    public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(DagRunStartKeyV1 startKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startKey);
        _ = new DagRunStartKeyV1(startKey.Value);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var run = dagRuns.Values.SingleOrDefault(r =>
                string.Equals(r.Snapshot.StartKey.Value, startKey.Value, StringComparison.Ordinal));
            return Task.FromResult(run is null
                ? Option<DagRunDiscoveryV1>.None
                : Option<DagRunDiscoveryV1>.Some(DagRunStoreRules.Discovery(run)));
        }
    }

    public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(WorkflowStreamId streamId,
        StreamVersion afterVersion, CancellationToken cancellationToken)
    {
        var id = DagRunStoreRules.RequireDagStream(streamId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<DagRunRecordV1> rows = dagRuns.TryGetValue(id, out var run)
                ? run.NewRecords.Where(r => r.StreamVersion.Value > afterVersion.Value)
                    .Select(r => DagRunRecordCodecV1.Deserialize(DagRunRecordCodecV1.Serialize(r))).ToArray()
                : [];
            return Task.FromResult(rows);
        }
    }

    public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(DagRunLeaseRequestV1 request,
        CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateLeaseRequest(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
            return Task.FromResult(DagRunStoreRules.Acquire(dagRuns.GetValueOrDefault(request.RunId), request,
                timeProvider.GetUtcNow(), () => checked(++dagLeaseSequence)));
    }

    public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(DagRunLeaseRequestV1 request,
        CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateLeaseRequest(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
            return Task.FromResult(DagRunStoreRules.Renew(dagRuns.GetValueOrDefault(request.RunId), request,
                timeProvider.GetUtcNow()));
    }

    public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(DagRunFenceV1 fence, CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateFence(fence);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
            return Task.FromResult(DagRunStoreRules.Release(dagRuns.GetValueOrDefault(fence.RunId), fence,
                timeProvider.GetUtcNow()));
    }

    public Task CancelDagRunLeaseRequestAsync(Guid runId, Guid ownerId, Guid requestId,
        CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateRequestCoordinates(runId, ownerId, requestId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
            DagRunStoreRules.CancelRequest(dagRuns.GetValueOrDefault(runId), ownerId, requestId,
                timeProvider.GetUtcNow());
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(DagRunDiscoveryRequestV1 request,
        CancellationToken cancellationToken)
    {
        DagRunStoreRules.ValidateDiscovery(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
            return Task.FromResult<IReadOnlyList<DagRunDiscoveryV1>>(dagRuns.Values
                .Where(r => r.CreationSequence > request.AfterCreationSequence &&
                            !DagRunStateReducer.Terminal(r.Snapshot.State))
                .OrderBy(r => r.CreationSequence).Take(request.MaxCount).Select(DagRunStoreRules.Discovery).ToArray());
    }

    public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(DagRunSubscriptionV1 subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (dagRuns.Values.Any(r => r.Observers.TryGetValue(subscription.SubscriptionId, out var existing) &&
                                        (existing.Subscription.RunId != subscription.RunId ||
                                         existing.Subscription.RecipientHostId != subscription.RecipientHostId)))
                throw new InvalidDataException("DAG_INTEGRITY_CONFLICT: subscription identity changed.");
            return Task.FromResult(DagRunStoreRules.Subscribe(dagRuns.GetValueOrDefault(subscription.RunId),
                subscription, timeProvider.GetUtcNow()));
        }
    }

    public Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            foreach (var run in dagRuns.Values)
                if (run.Observers.TryGetValue(subscriptionId, out var observer) &&
                    observer.Subscription.RecipientHostId == recipientHostId)
                    run.Observers[subscriptionId] = observer with
                    {
                        ExpiresAt = DagRunStoreRules.Clamp(run, timeProvider.GetUtcNow())
                    };
            PruneDagObservers(int.MaxValue);
        }

        return Task.CompletedTask;
    }

    public Task<int> PruneExpiredDagRunObserversAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        if (maxCount > 1000) throw new ArgumentOutOfRangeException(nameof(maxCount));
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate) return Task.FromResult(PruneDagObservers(maxCount));
    }

    private int PruneDagObservers(int maximum)
    {
        var removed = 0;
        foreach (var run in dagRuns.Values)
        {
            var now = DagRunStoreRules.Clamp(run, timeProvider.GetUtcNow());
            foreach (var observer in run.Observers.Values.Where(o => o.ExpiresAt <= now).ToArray())
            {
                if (removed == maximum) return removed;
                var notices = outbox.Values.Where(o => o.Write.Kind == DagRunStoreRules.ReadyKind &&
                                                       o.Write.DagRunOwner == run.Snapshot.RunId &&
                                                       o.Write.DagRunSubscriptionId ==
                                                       observer.Subscription.SubscriptionId &&
                                                       o.Write.RecipientHostId == observer.Subscription.RecipientHostId)
                    .ToArray();
                if (notices.Any(n => n.State == OutboxRecordState.Claimed && n.ClaimedUntil > now)) continue;
                foreach (var notice in notices.Where(n => n.State != OutboxRecordState.Poisoned))
                    outbox.Remove(notice.Write.OutboxRecordId);
                if (notices.Any(n => n.State == OutboxRecordState.Poisoned)) continue;
                run.Observers.Remove(observer.Subscription.SubscriptionId);
                removed++;
            }
        }

        return removed;
    }
}
