using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;

namespace OrcaCore.Hosting.Tests;

// A provider-port barrier, not a scheduler hook. Both commits reach the real provider
// while the caller is stopped after its subscription is registered and before recheck.
internal sealed class DagEventStoreProbe : IWorkflowEventStore
{
    internal IWorkflowEventStore Inner { get; set; } = null!;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource NextSubscription { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid heldRun;
    private int subscriptions;
    internal Func<DagRunCheckpointV1, DagRunCheckpointV1>? RewriteCheckpoint { get; set; }
    internal Func<CancellationToken, Task>? AfterKeyLookup { get; set; }
    internal System.Collections.Concurrent.ConcurrentQueue<string> AppendFailures { get; } = new();

    internal void HoldNextSubscription(Guid runId) => heldRun = runId;

    public async Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(DagRunSubscriptionV1 subscription, CancellationToken token)
    {
        var result = await Inner.SubscribeDagRunAsync(subscription, token);
        if (subscription.RunId == heldRun)
        {
            if (Interlocked.Increment(ref subscriptions) == 1)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(token);
            }
            else NextSubscription.TrySetResult();
        }
        return result;
    }

    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(InstanceId id, CancellationToken token) => Inner.LoadCheckpointAsync(id, token);
    public async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken token)
    {
        var result = await Inner.AppendAsync(batch, token);
        if (result.IsFailure) AppendFailures.Enqueue(result.Error.ToString());
        return result;
    }
    public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(WorkflowStreamId id, StreamVersion version, CancellationToken token) => Inner.LoadTailAsync(id, version, token);
    public async Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(WorkflowStreamId id, CancellationToken token)
    {
        var result = await Inner.LoadDagRunCheckpointAsync(id, token);
        return result.HasValue && RewriteCheckpoint is { } rewrite ? Option<DagRunCheckpointV1>.Some(rewrite(result.Value)) : result;
    }
    public async Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(DagRunStartKeyV1 key, CancellationToken token)
    {
        var result = await Inner.GetDagRunByStartKeyAsync(key, token);
        if (AfterKeyLookup is { } after) await after(token);
        return result;
    }
    public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(WorkflowStreamId id, StreamVersion version, CancellationToken token) => Inner.LoadDagRunTailAsync(id, version, token);
    public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(DagRunLeaseRequestV1 request, CancellationToken token) => Inner.AcquireDagRunLeaseAsync(request, token);
    public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(DagRunLeaseRequestV1 request, CancellationToken token) => Inner.RenewDagRunLeaseAsync(request, token);
    public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(DagRunFenceV1 fence, CancellationToken token) => Inner.ReleaseDagRunLeaseAsync(fence, token);
    public Task CancelDagRunLeaseRequestAsync(Guid run, Guid owner, Guid request, CancellationToken token) => Inner.CancelDagRunLeaseRequestAsync(run, owner, request, token);
    public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(DagRunDiscoveryRequestV1 request, CancellationToken token) => Inner.ListRecoverableDagRunsAsync(request, token);
    public Task UnsubscribeDagRunAsync(Guid subscription, Guid host, CancellationToken token) => Inner.UnsubscribeDagRunAsync(subscription, host, token);
    public Task<int> PruneExpiredDagRunObserversAsync(int maxCount, CancellationToken token) => Inner.PruneExpiredDagRunObserversAsync(maxCount, token);
}
