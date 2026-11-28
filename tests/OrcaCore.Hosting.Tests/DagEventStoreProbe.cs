using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;

namespace OrcaCore.Hosting.Tests;

// A provider-port barrier, not a scheduler hook. Both commits reach the real provider
// while the caller is stopped after its subscription is registered and before recheck.
internal class DagEventStoreProbe : IWorkflowEventStore
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

    internal Action<ProviderCommitBatch>? BeforeAppend { get; init; }
    internal Action<ProviderCommitBatch>? AfterAppend { get; init; }

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

    internal Func<CancellationToken, Task>? AfterCheckpointRead { get; init; }
    public async Task<Option<CheckpointWrite>> LoadCheckpointAsync(InstanceId id, CancellationToken token)
    {
        var current = await Inner.LoadCheckpointAsync(id, token);
        if (AfterCheckpointRead is not null) await AfterCheckpointRead(token);
        return current;
    }
    public virtual async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken token)
    {
        BeforeAppend?.Invoke(batch);
        var result = await Inner.AppendAsync(batch, token);
        if (result.IsFailure) AppendFailures.Enqueue(result.Error.ToString());
        else AfterAppend?.Invoke(batch);
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
    internal Action<DagRunFenceV1>? AfterLeaseRelease { get; init; }
    public async Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(DagRunFenceV1 fence, CancellationToken token)
    {
        var result = await Inner.ReleaseDagRunLeaseAsync(fence, token);
        AfterLeaseRelease?.Invoke(fence);
        return result;
    }
    public Task CancelDagRunLeaseRequestAsync(Guid run, Guid owner, Guid request, CancellationToken token) => Inner.CancelDagRunLeaseRequestAsync(run, owner, request, token);
    internal Func<CancellationToken, Task>? BeforeDiscovery { get; init; }
    public async Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(DagRunDiscoveryRequestV1 request, CancellationToken token)
    {
        if (BeforeDiscovery is { } before) await before(token);
        return await Inner.ListRecoverableDagRunsAsync(request, token);
    }
    public Task UnsubscribeDagRunAsync(Guid subscription, Guid host, CancellationToken token) => Inner.UnsubscribeDagRunAsync(subscription, host, token);
    public Task<int> PruneExpiredDagRunObserversAsync(int maxCount, CancellationToken token) => Inner.PruneExpiredDagRunObserversAsync(maxCount, token);
}

// Shared by the Hosting lane and both real-provider certifications. Only public
// facade/provider APIs are used here; the exact scheduler exception is observed.
internal sealed class DagIntegrityObserver : Microsoft.Extensions.Logging.ILoggerProvider, Microsoft.Extensions.Logging.ILogger
{
    internal TaskCompletionSource<Exception> Failure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (exception is InvalidDataException && formatter(state, exception).Contains("could not progress", StringComparison.Ordinal))
            Failure.TrySetResult(exception);
    }
    public void Dispose() { }
}

internal static class DagCanonicalIntegrityAssertions
{
    internal static string Error(string corruption) => corruption == "key"
        ? "DAG committed value has an invalid key, format or fingerprint."
        : "DAG committed run input is malformed.";

    internal static async Task RunAsync(Microsoft.Extensions.Hosting.IHost host, DagEventStoreProbe probe,
        DagIntegrityObserver observer, OrcaCore.Dag.DagDefinitionHandle<int> definition,
        OrcaCore.Dag.DagRunHandle run, StartIdempotencyKey key, string corruption, Func<int> mappingCalls,
        CancellationToken token)
    {
        var stream = WorkflowStreamId.ForDagRun(run.RunId.Value);
        var before = await probe.Inner.LoadDagRunTailAsync(stream, StreamVersion.Empty, token);
        probe.RewriteCheckpoint = checkpoint => checkpoint.RunId == run.RunId.Value
            ? OrcaCore.TestSupport.DagFacadeWire.CorruptRunInput(checkpoint, corruption) : checkpoint;
        try
        {
            async Task Reject(string operation, Func<Task> call)
            {
                var exception = await Xunit.Record.ExceptionAsync(call);
                exception.Should().BeOfType<InvalidDataException>(operation + " must reach run-input integrity validation").Which.Message.Should().Be(Error(corruption));
            }
            await Reject("reopen", () => definition.GetRunAsync(run.RunId, token).AsTask());
            await Reject("replay", () => definition.StartOrGetAsync(17, key, token).AsTask());
            // Corrupt representation is distinct from a valid checkpoint in IntegrityFault.
            await Reject("snapshot", () => run.GetSnapshotAsync(token).AsTask());
            await Reject("wait", () => run.WaitForTerminalAsync(token).AsTask());
            await Reject("cancel", () => run.RequestCancellationAsync(token).AsTask());
            await host.StartAsync(token);
            var error = await observer.Failure.Task.WaitAsync(token);
            error.Should().BeOfType<InvalidDataException>().Which.Message.Should().Be(Error(corruption));
            mappingCalls().Should().Be(0);
            (await probe.Inner.LoadDagRunTailAsync(stream, StreamVersion.Empty, token)).Should().BeEquivalentTo(before);
            var original = await OrcaCore.TestSupport.DagFacadeWire.FacadeReadAsync(probe.Inner, run.RunId.Value, token);
            original.Nodes.Should().ContainSingle().Which.State.Should().Be(OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1.Ready);
            original.State.Should().Be(OrcaCore.Abstractions.Durable.Dag.DagRunStateV1.Running);
            probe.RewriteCheckpoint = null;
            (await run.WaitForTerminalAsync(token)).Status.Should().Be(OrcaCore.Dag.DagRunStatus.Succeeded);
            mappingCalls().Should().Be(1, "the node is real and progresses once its original input is readable again");
        }
        finally
        {
            probe.RewriteCheckpoint = null;
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await host.StopAsync(stop.Token);
        }
    }
}
