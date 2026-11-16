using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagHostIdentity
{
    internal Guid HostId { get; } = Guid.CreateVersion7();
}

internal enum DurableDagTurnPurposeV1 { Admission, ChildJoin, RunCancellation, IntegrityReconciliation }
internal sealed record DurableDagTurnRequestV1(Guid RunId, Guid RequestId, DurableDagTurnPurposeV1 Purpose);
internal sealed record DurableDagTurnV1(DagRunFenceV1 Fence, StreamVersion Version,
    DateTimeOffset ExpiresAt, DateTimeOffset HardDeadline);
internal enum DurableDagTurnOutcomeV1 { Granted, Queued, OwnerBusy, LocalQueueFull, StaleFence, NotFound, Terminal }
internal sealed record DurableDagTurnResultV1(DurableDagTurnOutcomeV1 Outcome, DurableDagTurnV1? Turn,
    long? QueueSequence, long? LocalSequence);

// One boot-local FIFO shared by admission and the role-independent child pump.
// Only the head can submit a request to the provider.
internal sealed class DurableDagRunTurns(IWorkflowEventStore store, DurableDagHostIdentity host, TimeProvider time)
{
    internal const int MaximumLocalRequestsPerRun = 1024;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan WaiterHeartbeat = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(1);
    private readonly object gate = new();
    private readonly Dictionary<Guid, LinkedList<Entry>> queues = [];
    private readonly Dictionary<(Guid Run, Guid Request), Entry> requests = [];
    private long nextSequence;

    internal async ValueTask<DurableDagTurnResultV1> BeginTurnAsync(DurableDagTurnRequestV1 request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunId == Guid.Empty || request.RequestId == Guid.Empty || !Enum.IsDefined(request.Purpose))
            throw new ArgumentException("Invalid DAG turn coordinates.", nameof(request));
        token.ThrowIfCancellationRequested();
        Entry entry;
        lock (gate)
        {
            if (requests.TryGetValue((request.RunId, request.RequestId), out var existing))
            {
                if (existing.Request != request) throw new InvalidDataException("DAG turn request purpose changed.");
                entry = existing;
            }
            else
            {
                if (!queues.TryGetValue(request.RunId, out var queue)) queues.Add(request.RunId, queue = new());
                if (queue.Count >= MaximumLocalRequestsPerRun)
                    return new(DurableDagTurnOutcomeV1.LocalQueueFull, null, null, null);
                entry = new(request, checked(++nextSequence));
                entry.Node = queue.AddLast(entry);
                requests.Add((request.RunId, request.RequestId), entry);
            }
            if (entry.Node!.List!.First != entry.Node)
                return new(DurableDagTurnOutcomeV1.Queued, null, null, entry.Sequence);
        }
        await entry.Serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (entry.Node!.List is null) return Stale(entry);
            entry.Submitted = true;
            var result = await store.AcquireDagRunLeaseAsync(Lease(entry), token).ConfigureAwait(false);
            return Project(entry, result);
        }
        finally { entry.Serial.Release(); }
    }

    internal async ValueTask<DurableDagTurnResultV1> RenewTurnAsync(DurableDagTurnV1 turn, CancellationToken token)
    {
        var entry = Find(turn.Fence.RunId, turn.Fence.RequestId);
        if (entry is null) return Stale(null);
        await entry.Serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (entry.Node!.List is null || entry.Grant?.Fence != turn.Fence) return Stale(entry);
            var result = await store.RenewDagRunLeaseAsync(Lease(entry) with { ExpectedFence = turn.Fence.Token }, token)
                .ConfigureAwait(false);
            return Project(entry, result);
        }
        finally { entry.Serial.Release(); }
    }

    internal ValueTask EndTurnAsync(DurableDagTurnV1 turn, CancellationToken token) =>
        FinishAsync(turn.Fence.RunId, turn.Fence.RequestId, turn.Fence, null, token);

    internal ValueTask AbandonTurnRequestAsync(DurableDagTurnRequestV1 request, CancellationToken token) =>
        FinishAsync(request.RunId, request.RequestId, null, request, token);

    internal async ValueTask<bool> IsActiveAsync(DurableDagTurnV1 turn, CancellationToken token)
    {
        var entry = Find(turn.Fence.RunId, turn.Fence.RequestId);
        if (entry is null) return false;
        await entry.Serial.WaitAsync(token).ConfigureAwait(false);
        try { return entry.Node!.List is not null && entry.Grant?.Fence == turn.Fence; }
        finally { entry.Serial.Release(); }
    }

    private async ValueTask FinishAsync(Guid runId, Guid requestId, DagRunFenceV1? exactFence,
        DurableDagTurnRequestV1? exactRequest, CancellationToken token)
    {
        var entry = Find(runId, requestId);
        if (entry is null) return;
        await entry.Serial.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (entry.Node!.List is null || (exactFence is not null && entry.Grant?.Fence != exactFence) ||
                (exactRequest is not null && entry.Request != exactRequest)) return;
            try
            {
                if (entry.Grant is { } grant)
                    await store.ReleaseDagRunLeaseAsync(grant.Fence, token).ConfigureAwait(false);
                else if (entry.Submitted)
                    await store.CancelDagRunLeaseRequestAsync(runId, host.HostId, requestId, token).ConfigureAwait(false);
            }
            finally { Remove(entry); }
        }
        finally { entry.Serial.Release(); }
    }

    internal async Task<bool> ExecuteAsync(Guid runId, DagRunLeasePurposeV1 purpose,
        Func<DagRunFenceV1, CancellationToken, Task<bool>> action, CancellationToken token)
    {
        var request = new DurableDagTurnRequestV1(runId, Guid.CreateVersion7(), (DurableDagTurnPurposeV1)purpose);
        try
        {
            while (true)
            {
                var result = await BeginTurnAsync(request, token).ConfigureAwait(false);
                if (result.Outcome == DurableDagTurnOutcomeV1.Granted)
                {
                    var turn = result.Turn!;
                    using var lease = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var renewal = RenewWhileWorkingAsync(turn, lease);
                    try { return await action(turn.Fence, lease.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && lease.IsCancellationRequested)
                    { return false; }
                    finally
                    {
                        await lease.CancelAsync().ConfigureAwait(false);
                        await renewal.ConfigureAwait(false);
                    }
                }
                if (result.Outcome is not (DurableDagTurnOutcomeV1.Queued or DurableDagTurnOutcomeV1.OwnerBusy)) return false;
                await Task.Delay(WaiterHeartbeat, time, token).ConfigureAwait(false);
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30), time);
            await AbandonTurnRequestAsync(request, cleanup.Token).ConfigureAwait(false);
        }
    }

    private async Task RenewWhileWorkingAsync(DurableDagTurnV1 turn, CancellationTokenSource lease)
    {
        try
        {
            while (true)
            {
                await Task.Delay(RenewalInterval, time, lease.Token).ConfigureAwait(false);
                var renewed = await RenewTurnAsync(turn, lease.Token).ConfigureAwait(false);
                if (renewed.Outcome != DurableDagTurnOutcomeV1.Granted)
                {
                    await lease.CancelAsync().ConfigureAwait(false);
                    return;
                }
                turn = renewed.Turn!;
            }
        }
        catch (OperationCanceledException) when (lease.IsCancellationRequested) { }
        catch
        {
            await lease.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    private DagRunLeaseRequestV1 Lease(Entry entry) =>
        new(entry.Request.RunId, host.HostId, entry.Request.RequestId,
            (DagRunLeasePurposeV1)entry.Request.Purpose, null, LeaseDuration);

    private DurableDagTurnResultV1 Project(Entry entry, DagRunLeaseResultV1 result)
    {
        var outcome = result.Outcome switch
        {
            DagRunLeaseOutcomeV1.Granted => DurableDagTurnOutcomeV1.Granted,
            DagRunLeaseOutcomeV1.Queued => DurableDagTurnOutcomeV1.Queued,
            DagRunLeaseOutcomeV1.OwnerBusy => DurableDagTurnOutcomeV1.OwnerBusy,
            DagRunLeaseOutcomeV1.NotFound => DurableDagTurnOutcomeV1.NotFound,
            DagRunLeaseOutcomeV1.Terminal => DurableDagTurnOutcomeV1.Terminal,
            _ => DurableDagTurnOutcomeV1.StaleFence
        };
        if (outcome == DurableDagTurnOutcomeV1.Granted)
            entry.Grant = new(result.Fence!, result.CurrentVersion!.Value, result.ExpiresAt!.Value, result.HardDeadline!.Value);
        else if (outcome is not (DurableDagTurnOutcomeV1.Queued or DurableDagTurnOutcomeV1.OwnerBusy))
        {
            // A terminal acquire can still leave this host's provider waiter.
            // Keep its coordinates until the caller abandons the request.
            if (outcome != DurableDagTurnOutcomeV1.Terminal) Remove(entry);
        }
        return new(outcome, outcome == DurableDagTurnOutcomeV1.Granted ? entry.Grant : null, result.QueueSequence, entry.Sequence);
    }

    private Entry? Find(Guid run, Guid request)
    {
        lock (gate) return requests.GetValueOrDefault((run, request));
    }

    private void Remove(Entry entry)
    {
        lock (gate)
        {
            if (entry.Node!.List is not { } queue) return;
            queue.Remove(entry.Node);
            requests.Remove((entry.Request.RunId, entry.Request.RequestId));
            if (queue.Count == 0) queues.Remove(entry.Request.RunId);
        }
    }

    private static DurableDagTurnResultV1 Stale(Entry? entry) =>
        new(DurableDagTurnOutcomeV1.StaleFence, null, null, entry?.Sequence);

    private sealed class Entry(DurableDagTurnRequestV1 request, long sequence)
    {
        internal DurableDagTurnRequestV1 Request { get; } = request;
        internal long Sequence { get; } = sequence;
        internal SemaphoreSlim Serial { get; } = new(1, 1);
        internal LinkedListNode<Entry>? Node { get; set; }
        internal DurableDagTurnV1? Grant { get; set; }
        internal bool Submitted { get; set; }
    }
}
