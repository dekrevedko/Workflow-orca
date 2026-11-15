using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagNotifications(IWorkflowEventStore events, DurableDagRunStore runs,
    DurableDagHostIdentity host, TimeProvider time)
{
    private readonly ConcurrentDictionary<Guid, Watch> watches = new();
    private static readonly TimeSpan SubscriptionLease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(8);

    internal bool TryDeliver(DurableDagReadyNoticeV1 notice)
    {
        if (notice.SubscriptionId is not { } id || !watches.TryGetValue(id, out var watch) ||
            watch.RunId != notice.RunId || notice.Version.Value <= watch.AfterVersion.Value)
            return false;
        watch.Signal.TrySetResult();
        return true;
    }

    internal async ValueTask<DagRunSnapshotV1> WaitForTerminalAsync(Guid runId, CancellationToken token)
    {
        var snapshot = await ReadAsync(runId, token).ConfigureAwait(false);
        while (snapshot.State is not (DagRunStateV1.Succeeded or DagRunStateV1.Failed or DagRunStateV1.Cancelled))
            snapshot = await WaitForChangeAsync(runId, snapshot.StreamVersion, token).ConfigureAwait(false);
        return snapshot;
    }

    internal async ValueTask<DagRunSnapshotV1> WaitForChangeAsync(Guid runId, StreamVersion afterVersion, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var id = Guid.CreateVersion7();
            var watch = new Watch(runId, afterVersion);
            if (!watches.TryAdd(id, watch)) throw new InvalidOperationException("DAG observer identity collision.");
            var subscription = new DagRunSubscriptionV1(id, host.HostId, runId, afterVersion, SubscriptionLease);
            try
            {
                var subscribed = await events.SubscribeDagRunAsync(subscription, token).ConfigureAwait(false);
                if (subscribed.Outcome == DagRunSubscribeOutcomeV1.NotFound)
                    throw new InvalidOperationException("DAG run does not exist.");
                var current = await ReadAsync(runId, token).ConfigureAwait(false);
                if (current.StreamVersion.Value > afterVersion.Value) return current;
                while (!watch.Signal.Task.IsCompleted)
                {
                    using var timer = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var delay = Task.Delay(RenewalInterval, time, timer.Token);
                    var completed = await Task.WhenAny(watch.Signal.Task, delay).ConfigureAwait(false);
                    await timer.CancelAsync().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (completed == watch.Signal.Task) break;
                    var renewed = await events.SubscribeDagRunAsync(subscription, token).ConfigureAwait(false);
                    if (renewed.Outcome == DagRunSubscribeOutcomeV1.AlreadyChanged) break;
                    if (renewed.Outcome == DagRunSubscribeOutcomeV1.NotFound)
                        throw new InvalidOperationException("DAG run no longer exists.");
                }
                current = await ReadAsync(runId, token).ConfigureAwait(false);
                if (current.StreamVersion.Value > afterVersion.Value) return current;
            }
            finally
            {
                watches.TryRemove(id, out _);
                using var cleanup = new CancellationTokenSource(SubscriptionLease, time);
                await events.UnsubscribeDagRunAsync(id, host.HostId, cleanup.Token).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<DagRunSnapshotV1> ReadAsync(Guid id, CancellationToken token) =>
        await runs.ReadAsync(id, token).ConfigureAwait(false) ?? throw new InvalidOperationException("DAG run does not exist.");

    private sealed record Watch(Guid RunId, StreamVersion AfterVersion)
    {
        internal TaskCompletionSource Signal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
