using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dag;
using OrcaCore.Hosting.Tests;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static partial class DagRunCertification
{
    public static async Task RemoteJoinAfterLostWakeAsync(Func<IServiceCollection> provider, bool restartScheduler, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        var graph = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var rootThread = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dependentThread = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var root = graph.Node(DagNodeId.Create("root"), child.Reference).MapInput(input =>
        {
            if (input.RunInput == 41) rootThread.TrySetResult(Thread.CurrentThread);
            return input.RunInput;
        });
        graph.Node(DagNodeId.Create("dependent"), child.Reference).DependsOn(root).MapInput(input =>
        {
            if (input.RunInput == 41) dependentThread.TrySetResult(Thread.CurrentThread);
            return input.OutputOf(root) + 1;
        });
        var plan = graph.Build();

        // Only the worker host consumes the real outbox. The scheduler still writes real
        // child intents and queries the real discovery index and checkpoints.
        IServiceCollection SchedulerProvider() => provider().AddSingleton<IWorkflowOutboxStore>(new FakeWorkflowEventStore());
        var rootAdmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dependentAdmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Guid target = default;

        void ObserveAdmission(ProviderCommitBatch batch)
        {
            if (batch.DagRun?.Records.FirstOrDefault(r => r.RunId == target && r.Kind == DagRunRecordKindV1.InputAdmitted) is not { } record) return;
            var ordinal = Assert.Single(record.ChangedNodes).Ordinal;
            (ordinal == 0 ? rootAdmitted : dependentAdmitted).TrySetResult();
        }

        using var passes = new SemaphoreSlim(0);
        var barriers = new ConcurrentDictionary<Guid, TaskCompletionSource>();
        var reads = 0;
        var rootTurnReleased = false;
        var localCompletionVisit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerProbe = new DagEventStoreProbe
        {
            BeforeDiscovery = passes.WaitAsync,
            AfterLeaseRelease = fence =>
            {
                if (fence.RunId == target) Volatile.Write(ref rootTurnReleased, true);
            },
            RewriteCheckpoint = checkpoint =>
            {
                if (checkpoint.RunId == target)
                {
                    Interlocked.Increment(ref reads);
                    if (Volatile.Read(ref rootTurnReleased)) localCompletionVisit.TrySetResult();
                }
                if (barriers.TryGetValue(checkpoint.RunId, out var barrier)) barrier.TrySetResult();
                return checkpoint;
            },
            AfterAppend = ObserveAdmission
        };
        using var reader = FacadeHost(SchedulerProvider(), readerProbe, child, dagLimit: 2);
        var markers = reader.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();

        async Task AdmitRootAsync(IHost host, DagEventStoreProbe probe, SemaphoreSlim discovery)
        {
            var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
            target = (await definition.StartOrGetAsync(41, StartIdempotencyKey.Create("cache-target"), token)).GetHandleOrThrow().RunId.Value;
            discovery.Release();
            await rootAdmitted.Task.WaitAsync(token);
            var admitted = await FacadeReadAsync(probe.Inner, target, token);
            Assert.Equal(new[] { DagNodeStateV1.StartAuthorized, DagNodeStateV1.Pending }, admitted.Nodes.Select(n => n.State));
        }

        async Task ObservePassAsync(string key)
        {
            // Discovery and dispatch preserve creation order. Once the later marker is read,
            // any read enqueued for the target in this pass has already completed.
            var marker = (await markers.StartOrGetAsync(0, StartIdempotencyKey.Create(key), token)).GetHandleOrThrow();
            var visited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            barriers[marker.RunId.Value] = visited;
            passes.Release();
            await visited.Task.WaitAsync(token);
        }

        var joined = new TaskCompletionSource<StreamVersion>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workerProbe = new DagEventStoreProbe
        {
            AfterAppend = batch =>
            {
                if (batch.DagRun?.Records.FirstOrDefault(r => r.RunId == target && r.Kind == DagRunRecordKindV1.ChildJoined) is { } join)
                    joined.TrySetResult(join.StreamVersion);
            }
        };
        using var worker = FacadeHost(provider(), workerProbe, child, dagLimit: null);
        if (restartScheduler)
        {
            using var seedPass = new SemaphoreSlim(0);
            var seedProbe = new DagEventStoreProbe { AfterAppend = ObserveAdmission, BeforeDiscovery = seedPass.WaitAsync };
            using var seed = FacadeHost(SchedulerProvider(), seedProbe, child, dagLimit: 2);
            await seed.StartAsync(token);
            try { await AdmitRootAsync(seed, seedProbe, seedPass); }
            finally { await StopFacadeHost(seed); }
        }
        await reader.StartAsync(token);
        try
        {
            if (!restartScheduler)
            {
                await AdmitRootAsync(reader, readerProbe, passes);
                // Let the admission task release its turn and trigger its final local visit.
                // The next reads can then be attributed to the controlled discovery passes.
                await localCompletionVisit.Task.WaitAsync(token);
            }
            await ObservePassAsync("warm-cache");
            var warmReads = Volatile.Read(ref reads);
            if (restartScheduler) Assert.Equal(1, warmReads);
            await ObservePassAsync("same-version");
            Assert.Equal(warmReads, Volatile.Read(ref reads));
            var original = await rootThread.Task.WaitAsync(token);
            Assert.True(original.Join(TimeSpan.FromSeconds(5)), "the root mapper thread should exit while its child remains admitted");

            await worker.StartAsync(token);
            try
            {
                var version = await joined.Task.WaitAsync(token);
                var snapshot = await FacadeReadAsync(workerProbe.Inner, target, token);
                Assert.Equal(version, snapshot.StreamVersion);
                Assert.Equal(new[] { DagNodeStateV1.Succeeded, DagNodeStateV1.Ready }, snapshot.Nodes.Select(n => n.State));

                // Simulate a lost notification after a successful claim and acknowledgement.
                // Discovery is held until the notice for this specific join has disappeared.
                var outbox = worker.Services.GetRequiredService<IWorkflowOutboxStore>();
                var notices = await outbox.ClaimAsync(new OutboxClaimRequest(100, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30))
                    { KindSelector = OutboxKindSelector.Including("dag-run-ready-v1") }, token);
                var lost = Assert.Single(notices, w => w.DagRunOwner == target && w.RecipientHostId is null
                    && w.DagRunSubscriptionId is null && JsonSerializer.Deserialize<JsonElement>(w.Payload).GetProperty("version").GetInt64() == version.Value);
                Assert.True(await outbox.MarkAsync(lost.OutboxRecordId, lost.DispatchAttempt, OutboxRecordState.Dispatched, token));
                await ObservePassAsync("changed-version");
                Assert.True(Volatile.Read(ref reads) > warmReads, "discovery must reread the changed run before dispatching the later marker");
                await dependentAdmitted.Task.WaitAsync(token);
                Assert.NotSame(original, await dependentThread.Task.WaitAsync(token));
                var tail = await workerProbe.Inner.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(target), version, token);
                var admission = Assert.Single(tail, r => r.Kind == DagRunRecordKindV1.InputAdmitted);
                var node = Assert.Single(admission.ChangedNodes);
                Assert.Equal(1, node.Ordinal);
                Assert.Equal("42"u8.ToArray(), node.InputWrite!.Bytes);
            }
            finally { await StopFacadeHost(worker); }
        }
        finally { await StopFacadeHost(reader); }
    }
}
