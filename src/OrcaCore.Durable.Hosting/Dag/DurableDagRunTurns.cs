using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagHostIdentity
{
    internal Guid HostId { get; } = Guid.CreateVersion7();
}

// Shared by every local purpose, including the later admission bridge. No run owns
// a provider lease while it is waiting in this bounded, arrival-ordered local FIFO.
internal sealed class DurableDagRunTurns(IWorkflowEventStore store, DurableDagHostIdentity host, TimeProvider time)
{
    internal const int MaximumLocalRequestsPerRun = 64;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WaiterHeartbeat = TimeSpan.FromMilliseconds(250);
    private readonly object gate = new();
    private readonly Dictionary<Guid, Queue<TaskCompletionSource>> queues = [];

    internal async Task<bool> ExecuteAsync(Guid runId, DagRunLeasePurposeV1 purpose,
        Func<DagRunFenceV1, CancellationToken, Task<bool>> action, CancellationToken token)
    {
        var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (!queues.TryGetValue(runId, out var queue)) queues.Add(runId, queue = new());
            if (queue.Count >= MaximumLocalRequestsPerRun) return false; // LocalQueueFull: retain the outbox work.
            queue.Enqueue(waiter);
            if (queue.Count == 1) waiter.SetResult();
        }
        DagRunFenceV1? fence = null;
        var request = new DagRunLeaseRequestV1(runId, host.HostId, Guid.CreateVersion7(), purpose, null, LeaseDuration);
        try
        {
            await waiter.Task.WaitAsync(token).ConfigureAwait(false);
            while (true)
            {
                var lease = await store.AcquireDagRunLeaseAsync(request, token).ConfigureAwait(false);
                if (lease.Outcome == DagRunLeaseOutcomeV1.Granted)
                {
                    fence = lease.Fence!;
                    return await action(fence, token).ConfigureAwait(false);
                }
                if (lease.Outcome is not (DagRunLeaseOutcomeV1.Queued or DagRunLeaseOutcomeV1.OwnerBusy)) return false;
                await Task.Delay(WaiterHeartbeat, time, token).ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                // This request can never release a newer request's grant. Provider receipt
                // replay and fence CAS govern cleanup even when an append response was lost.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30), time);
                if (fence is not null) await store.ReleaseDagRunLeaseAsync(fence, cleanup.Token).ConfigureAwait(false);
                else await store.CancelDagRunLeaseRequestAsync(runId, host.HostId, request.RequestId, cleanup.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (gate)
                {
                    var queue = queues[runId];
                    // Cancelled local waiters are removed without disturbing arrival order.
                    var remaining = queue.Where(item => !ReferenceEquals(item, waiter)).ToArray();
                    queue.Clear();
                    foreach (var item in remaining) queue.Enqueue(item);
                    if (queue.Count == 0) queues.Remove(runId);
                    else queue.Peek().TrySetResult();
                }
            }
        }
    }
}
