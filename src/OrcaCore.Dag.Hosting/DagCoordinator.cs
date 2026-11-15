using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Durable.Hosting.Dag;

namespace OrcaCore.Dag.Hosting;

internal sealed class DagCoordinator : IDurableDagReadySinkV1, IDurableDagProgressionLoop, IDisposable
{
    internal const int DagEvaluationParallelismPerRun = 1;
    private const int DiscoveryPageSize = 100;
    private readonly DurableDagRunBridge bridge;
    private readonly TimeProvider time;
    private readonly ILogger<DagCoordinator> logger;
    private readonly ConcurrentDictionary<Guid, Lane> lanes = new();
    private readonly Channel<Wake> wakes = Channel.CreateBounded<Wake>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });
    private readonly ConcurrentDictionary<Guid, Wake> enqueued = new();
    private readonly Meter meter = new("OrcaCore.Dag.Hosting");
    private int activeWorkers;
    private int retiredWorkers;

    internal DagCoordinator(DagDefinitionRegistry definitions, DurableDagRunBridge bridge, int maxConcurrentNodes,
        TimeProvider time, ILogger<DagCoordinator> logger)
    {
        Definitions = definitions;
        this.bridge = bridge;
        this.time = time;
        this.logger = logger;
        MaxConcurrentNodes = maxConcurrentNodes;
        meter.CreateObservableGauge("orca.dag.evaluation.lanes.active", () => Volatile.Read(ref activeWorkers));
        meter.CreateObservableGauge("orca.dag.evaluation.lanes.retired", () => Volatile.Read(ref retiredWorkers));
    }

    internal DagDefinitionRegistry Definitions { get; }
    internal int MaxConcurrentNodes { get; }

    internal ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync<TRunInput>(WorkflowDagPlan<TRunInput> plan,
        Guid runId, string startKey, TRunInput input, CancellationToken token)
    {
        var installed = Definitions.Install(plan);
        return bridge.CreateOrReopenAsync(new(runId, new(startKey), installed.Graph, input, installed.InputType), token);
    }

    ValueTask IDurableDagReadySinkV1.HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return AcceptAsync(new Wake(notice.RunId, notice), cancellationToken);
    }

    void IDurableDagProgressionLoop.CompleteWake(DurableDagReadyNoticeV1 notice, bool acknowledged)
    {
        if (enqueued.TryGetValue(notice.RunId, out var wake) && ReferenceEquals(wake.Notice, notice))
            wake.Accepted.TrySetResult(acknowledged);
    }

    public Task RunAsync(CancellationToken token) => Task.WhenAll(DiscoverAsync(token), DispatchAsync(token));

    private void Enqueue(Guid run)
    {
        var wake = new Wake(run, null);
        if (!enqueued.TryAdd(run, wake)) return;
        // Saturation coalesces to the next authoritative discovery pass rather
        // than blocking a ready claim behind unrelated user code.
        if (!wakes.Writer.TryWrite(wake)) enqueued.TryRemove(run, out _);
    }

    private async Task DiscoverAsync(CancellationToken token)
    {
        while (true)
        {
            try
            {
                long after = 0;
                var seen = new HashSet<Guid>();
                while (true)
                {
                    var page = await bridge.DiscoverAsync(after, DiscoveryPageSize, token).ConfigureAwait(false);
                    foreach (var run in page)
                    {
                        seen.Add(run.RunId);
                        if (Definitions.Find(run.Definition) is null) continue;
                        if (lanes.TryGetValue(run.RunId, out var lane) &&
                            Volatile.Read(ref lane.CachedVersion) == run.StreamVersion.Value && lane.CanSkipUnchanged) continue;
                        await EnqueueDiscoveryAsync(run.RunId, token).ConfigureAwait(false);
                    }
                    if (page.Count < DiscoveryPageSize) break;
                    after = page[^1].CreationSequence;
                }
                // Terminal runs leave the discovery index. Recheck their lanes to
                // retire live evaluations and release idle dedicated workers.
                foreach (var run in lanes.Keys.Where(run => !seen.Contains(run)))
                    await EnqueueDiscoveryAsync(run, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning(error, "DAG recovery discovery failed; retrying."); }
            await Task.Delay(TimeSpan.FromSeconds(4), time, token).ConfigureAwait(false);
        }
    }

    private ValueTask EnqueueDiscoveryAsync(Guid run, CancellationToken token) => AcceptAsync(new Wake(run, null), token);

    private async ValueTask AcceptAsync(Wake wake, CancellationToken token)
    {
        if (!enqueued.TryAdd(wake.RunId, wake)) return;
        try { await wakes.Writer.WriteAsync(wake, token).ConfigureAwait(false); }
        catch { enqueued.TryRemove(wake.RunId, out _); throw; }
    }

    private async Task DispatchAsync(CancellationToken token)
    {
        try
        {
            await foreach (var wake in wakes.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                var runId = wake.RunId;
                var accepted = wake.Notice is null || await wake.Accepted.Task.WaitAsync(token).ConfigureAwait(false);
                enqueued.TryRemove(runId, out _);
                if (!accepted) continue;
                try { await VisitAsync(runId, token).ConfigureAwait(false); }
                catch (DurableDagDefinitionUnavailableException) { }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception error) { logger.LogWarning(error, "DAG run {RunId} could not progress.", runId); }
            }
        }
        finally
        {
            foreach (var (id, lane) in lanes) Retire(id, lane);
        }
    }

    private async ValueTask VisitAsync(Guid runId, CancellationToken token)
    {
        var run = await bridge.ReadAsync(runId, token).ConfigureAwait(false);
        if (run?.State != DagRunStateV1.Running || Definitions.Find(run.Graph.Definition) is not { } plan)
        {
            if (lanes.TryGetValue(runId, out var old)) Retire(runId, old);
            return;
        }
        if (!bridge.MatchesInstalledGraph(run.Graph, plan.Graph))
            throw new InvalidDataException("Persisted DAG graph differs from the installed plan.");
        var lane = lanes.GetOrAdd(runId, static _ => new Lane());
        if (lane.Retired)
        {
            if (lane.Work is { IsCompleted: false }) return;
            lane = new Lane();
            lanes[runId] = lane;
        }
        Volatile.Write(ref lane.CachedVersion, run.StreamVersion.Value);
        var ready = run.Nodes.FirstOrDefault(node => node.State == DagNodeStateV1.Ready);
        var full = run.Nodes.Count(node => node.State is DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested)
            >= MaxConcurrentNodes;
        lane.CanSkipUnchanged = full || lane.Work is { IsCompleted: false } || lane.Prepared is not null;
        if (lane.Work is { IsCompleted: false }) return;
        if (lane.Work is { } finished)
        {
            lane.Work = null;
            try { lane.Prepared = await finished.ConfigureAwait(false); }
            catch
            {
                lane.CanSkipUnchanged = false;
                throw;
            }
        }
        if (lane.Prepared is { } prepared && ready?.Ordinal != prepared.Node.Identity.AuthoredOrdinal)
            lane.Prepared = null; // Resolution or mandatory new-lower-ordinal invalidation.
        if (ready is null)
        {
            lane.CanSkipUnchanged = full;
            return;
        }
        if (lane.Prepared is { } candidate)
        {
            if (full && !candidate.IsMappingInvalid) return;
            lane.Work = AdmitAsync(runId, candidate, token);
            WakeWhenFinished(runId, lane, lane.Work);
        }
        else if (!full)
        {
            var ordinal = ready.Ordinal;
            lane.Worker ??= new EvaluationWorker(this);
            lane.Work = lane.Worker.Submit(() => Evaluate(plan, runId, ordinal, token));
            WakeWhenFinished(runId, lane, lane.Work);
        }
    }

    private void WakeWhenFinished(Guid id, Lane lane, Task work) => _ = work.ContinueWith(completed =>
    {
        _ = completed.Exception; // Observe errors even if shutdown retired the lane.
        lane.CanSkipUnchanged = false; // A full wake channel cannot hide completed work from discovery.
        Enqueue(id);
    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private DurableDagPreparedMappingV1 Evaluate(InstalledDagPlan plan, Guid runId, int ordinal, CancellationToken token)
    {
        var node = plan.NodeContract(runId, ordinal);
        var inputs = bridge.OpenChildBridgeForEvaluation().ReadMappingInputsAsync(node, token).AsTask().GetAwaiter().GetResult();
        var evaluated = inputs is DurableDagMappingInputsV1.Available available ? plan.Evaluate(ordinal, available) : null;
        var mapped = evaluated is { IsValid: true, InputType: { } type } ? new DurableDagMappedInputV1(evaluated.Input, type) : null;
        return bridge.PrepareMappingAsync(node, mapped, mapped is null, token).AsTask().GetAwaiter().GetResult();
    }

    private async Task<DurableDagPreparedMappingV1?> AdmitAsync(Guid id, DurableDagPreparedMappingV1 receipt,
        CancellationToken token)
    {
        var request = new DurableDagTurnRequestV1(id, Guid.CreateVersion7(), DurableDagTurnPurposeV1.Admission);
        try
        {
            while (true)
            {
                var result = await bridge.BeginTurnAsync(request, token).ConfigureAwait(false);
                if (result.Turn is { } turn)
                {
                    using var lease = CancellationTokenSource.CreateLinkedTokenSource(token);
                    var renew = RenewAsync(turn, lease);
                    try
                    {
                        var admitted = await bridge.AdmitPreparedMappingAsync(receipt, turn,
                            MaxConcurrentNodes, lease.Token).ConfigureAwait(false);
                        return admitted.Outcome is DurableDagMutationOutcomeV1.CapacityDeferred or
                            DurableDagMutationOutcomeV1.StaleFence or DurableDagMutationOutcomeV1.VersionConflict ? receipt : null;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && lease.IsCancellationRequested)
                    { return receipt; }
                    finally
                    {
                        await lease.CancelAsync().ConfigureAwait(false);
                        await renew.ConfigureAwait(false);
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30), time);
                        await bridge.EndTurnAsync(turn, cleanup.Token).ConfigureAwait(false);
                    }
                }
                if (result.Outcome is DurableDagTurnOutcomeV1.Terminal or DurableDagTurnOutcomeV1.NotFound) return null;
                if (result.Outcome is DurableDagTurnOutcomeV1.StaleFence or DurableDagTurnOutcomeV1.LocalQueueFull)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), time, token).ConfigureAwait(false);
                    return receipt;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250), time, token).ConfigureAwait(false);
            }
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30), time);
            await bridge.AbandonTurnRequestAsync(request, cleanup.Token).ConfigureAwait(false);
        }
    }

    private async Task RenewAsync(DurableDagTurnV1 turn, CancellationTokenSource lease)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), time, lease.Token).ConfigureAwait(false);
                var renewed = await bridge.RenewTurnAsync(turn, lease.Token).ConfigureAwait(false);
                if (renewed.Turn is not { } fresh)
                {
                    await lease.CancelAsync().ConfigureAwait(false);
                    return;
                }
                turn = fresh;
            }
        }
        catch (OperationCanceledException) when (lease.IsCancellationRequested) { }
        catch
        {
            await lease.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void Retire(Guid runId, Lane lane)
    {
        // A retired worker stays associated with its run until the physical
        // mapper returns; do not replace a cancelled but still executing lane.
        lane.Retired = true;
        lane.Worker?.Retire();
        lane.Prepared = null;
        if (lane.Work is not { IsCompleted: false }) lanes.TryRemove(runId, out _);
    }

    public void Dispose()
    {
        foreach (var lane in lanes.Values) lane.Worker?.Retire();
        meter.Dispose();
    }

    private sealed class Lane
    {
        internal long CachedVersion;
        internal volatile bool CanSkipUnchanged;
        internal bool Retired { get; set; }
        internal EvaluationWorker? Worker { get; set; }
        internal Task<DurableDagPreparedMappingV1?>? Work { get; set; }
        internal DurableDagPreparedMappingV1? Prepared { get; set; }
    }

    private sealed record Wake(Guid RunId, DurableDagReadyNoticeV1? Notice)
    {
        internal TaskCompletionSource<bool> Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class EvaluationWorker
    {
        private readonly DagCoordinator owner;
        private readonly System.Collections.Concurrent.BlockingCollection<Action> queue = new(DagEvaluationParallelismPerRun);
        private readonly object gate = new();
        private bool retired;
        private bool exited;

        internal EvaluationWorker(DagCoordinator owner)
        {
            this.owner = owner;
            Interlocked.Increment(ref owner.activeWorkers);
            var worker = new Thread(Run) { IsBackground = true, Name = "OrcaCore DAG evaluation" };
            worker.Start();
        }

        internal Task<DurableDagPreparedMappingV1?> Submit(Func<DurableDagPreparedMappingV1> action)
        {
            var completion = new TaskCompletionSource<DurableDagPreparedMappingV1?>(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.Add(() =>
            {
                try { completion.SetResult(action()); }
                catch (Exception error) { completion.SetException(error); }
            });
            return completion.Task;
        }

        internal void Retire()
        {
            lock (gate)
            {
                if (retired || exited) return;
                retired = true;
                Interlocked.Decrement(ref owner.activeWorkers);
                Interlocked.Increment(ref owner.retiredWorkers);
                queue.CompleteAdding();
            }
        }

        private void Run()
        {
            try { foreach (var action in queue.GetConsumingEnumerable()) action(); }
            finally
            {
                lock (gate)
                {
                    exited = true;
                    if (retired) Interlocked.Decrement(ref owner.retiredWorkers);
                    else Interlocked.Decrement(ref owner.activeWorkers);
                }
                queue.Dispose();
            }
        }
    }
}
