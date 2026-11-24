using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
    public static async Task RemoteJoinAfterLostWakeAsync(Func<IServiceCollection> provider, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        token = timeout.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        var graph = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var root = graph.Node(DagNodeId.Create("root"), child.Reference).MapInput(input => input.RunInput);
        graph.Node(DagNodeId.Create("dependent"), child.Reference).DependsOn(root).MapInput(input => input.OutputOf(root) + 1);
        var plan = graph.Build();

        // Only the worker host consumes the real outbox. The scheduler still writes real
        // child intents and queries the real discovery index and checkpoints.
        IServiceCollection SchedulerProvider() => provider().AddSingleton<IWorkflowOutboxStore>(new FakeWorkflowEventStore());
        var rootAdmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seedProbe = new DagEventStoreProbe
        {
            AfterAppend = batch =>
            {
                if (batch.DagRun?.Records.Any(r => r.Kind == DagRunRecordKindV1.InputAdmitted) == true)
                    rootAdmitted.TrySetResult();
            }
        };
        Guid target;
        using (var seed = FacadeHost(SchedulerProvider(), seedProbe, child, dagLimit: 2))
        {
            await seed.StartAsync(token);
            try
            {
                var definition = seed.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
                target = (await definition.StartOrGetAsync(41, StartIdempotencyKey.Create("cache-target"), token)).GetHandleOrThrow().RunId.Value;
                await rootAdmitted.Task.WaitAsync(token);
                var snapshot = await FacadeReadAsync(seedProbe.Inner, target, token);
                Assert.Equal(new[] { DagNodeStateV1.StartAuthorized, DagNodeStateV1.Pending }, snapshot.Nodes.Select(n => n.State));
            }
            finally { await StopFacadeHost(seed); }
        }

        using var passes = new SemaphoreSlim(0);
        var barriers = new ConcurrentDictionary<Guid, TaskCompletionSource>();
        var reads = 0;
        var dependentAdmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readerProbe = new DagEventStoreProbe
        {
            BeforeDiscovery = passes.WaitAsync,
            RewriteCheckpoint = checkpoint =>
            {
                if (checkpoint.RunId == target) Interlocked.Increment(ref reads);
                if (barriers.TryGetValue(checkpoint.RunId, out var barrier)) barrier.TrySetResult();
                return checkpoint;
            },
            AfterAppend = batch =>
            {
                if (batch.DagRun?.Records.Any(r => r.RunId == target && r.Kind == DagRunRecordKindV1.InputAdmitted
                    && r.ChangedNodes.Any(n => n.Ordinal == 1)) == true) dependentAdmitted.TrySetResult();
            }
        };
        using var reader = FacadeHost(SchedulerProvider(), readerProbe, child, dagLimit: 2);
        var markers = reader.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();

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
        await reader.StartAsync(token);
        try
        {
            await ObservePassAsync("warm-cache");
            Assert.Equal(1, Volatile.Read(ref reads));
            await ObservePassAsync("same-version");
            Assert.Equal(1, Volatile.Read(ref reads));

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
                Assert.True(Volatile.Read(ref reads) > 1, "discovery must reread the changed run before dispatching the later marker");
                await dependentAdmitted.Task.WaitAsync(token);
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
