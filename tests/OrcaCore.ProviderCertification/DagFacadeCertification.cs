using System.Security.Cryptography;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Tests;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.ProviderCertification;

public static partial class DagRunCertification
{
    public sealed class FacadeLossyInput
    {
        public int Value { get; private set; }
        public static FacadeLossyInput Create(int value) => new() { Value = value };
    }

    // A new host's codec reads the same recorded shape differently. The representation
    // check must reject it even though the type identity and digest remain unchanged.
    public sealed class FacadeRedeployedInput
    {
        public static volatile bool DropOnRead;
        private int value;
        public int Value { get => value; set { if (!DropOnRead) this.value = value; } }
    }

    public static async Task FacadeBindingsAsync(Func<IServiceCollection> provider, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        token = deadline.Token;
        var probe = new DagEventStoreProbe();
        using var first = FacadeHost(provider(), probe);
        await first.StartAsync(token);
        using var second = FacadeHost(provider());
        await second.StartAsync(token);
        try
        {
            var id = DefinitionId.New();
            var plan = global::OrcaCore.Dag.Dag.Define<int>(id, DefinitionVersion.Initial).Build();
            var a = first.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
            first.Services.GetRequiredService<IDagDefinitionRegistry>()
                .Register(global::OrcaCore.Dag.Dag.Define<string>(id, DefinitionVersion.Initial).Build())
                .Should().BeOfType<DagRegistrationResult<string>.RunInputConflict>();
            var key = StartIdempotencyKey.Create("cert-bind-" + Guid.NewGuid().ToString("N"));
            var run = (await a.StartOrGetAsync(17, key, token)).GetHandleOrThrow();
            var replay = (await a.StartOrGetAsync(17, key, token)).Should().BeOfType<DagStartResult.Accepted>().Subject;
            replay.WasExisting.Should().BeTrue(); replay.Handle.RunId.Should().Be(run.RunId);
            (await a.StartOrGetAsync(18, key, token)).Should().BeOfType<DagStartResult.Conflict>();
            var b = second.Services.GetRequiredService<IDagDefinitionRegistry>()
                .Register(global::OrcaCore.Dag.Dag.Define<string>(id, DefinitionVersion.Initial).Build()).GetHandleOrThrow();
            (await b.StartOrGetAsync("17", key, token)).Should().BeOfType<DagStartResult.RunInputConflict>()
                .Which.ExistingRunId.Should().Be(run.RunId);
            Func<Task> incompatible = () => b.GetRunAsync(run.RunId, token).AsTask();
            await incompatible.Should().ThrowAsync<DagRunInputContractMismatchException>();

            var lossy = first.Services.GetRequiredService<IDagDefinitionRegistry>()
                .Register(global::OrcaCore.Dag.Dag.Define<FacadeLossyInput>(DefinitionId.New(), DefinitionVersion.Initial).Build()).GetHandleOrThrow();
            var lossyKey = StartIdempotencyKey.Create("cert-lossy-" + Guid.NewGuid().ToString("N"));
            Func<Task> reject = () => lossy.StartOrGetAsync(FacadeLossyInput.Create(17), lossyKey, token).AsTask();
            await reject.Should().ThrowAsync<NotSupportedException>();
            (await probe.Inner.GetDagRunByStartKeyAsync(new(lossyKey.Value), token)).HasValue.Should().BeFalse();
            var zero = (await lossy.StartOrGetAsync(FacadeLossyInput.Create(0), lossyKey, token)).GetHandleOrThrow();
            await reject.Should().ThrowAsync<NotSupportedException>();
            (await lossy.GetRunAsync(zero.RunId, token)).RunId.Should().Be(zero.RunId);

            // Corrupt value key versus its OWN graph must never turn into a closed mismatch.
            probe.RewriteCheckpoint = checkpoint =>
            {
                if (checkpoint.RunId != run.RunId.Value) return checkpoint;
                return OrcaCore.TestSupport.DagFacadeWire.CorruptRunInput(checkpoint, "key");
            };
            Func<Task> corruptReopen = () => a.GetRunAsync(run.RunId, token).AsTask();
            Func<Task> corruptReplay = () => a.StartOrGetAsync(17, key, token).AsTask();
            (await corruptReopen.Should().ThrowAsync<InvalidDataException>()).Which.Message.Should().Be(DagCanonicalIntegrityAssertions.Error("key"));
            (await corruptReplay.Should().ThrowAsync<InvalidDataException>()).Which.Message.Should().Be(DagCanonicalIntegrityAssertions.Error("key"));
            probe.RewriteCheckpoint = null;

            FacadeRedeployedInput.DropOnRead = false;
            var typedPlan = global::OrcaCore.Dag.Dag.Define<FacadeRedeployedInput>(DefinitionId.New(), DefinitionVersion.Initial).Build();
            var typedA = first.Services.GetRequiredService<IDagDefinitionRegistry>().Register(typedPlan).GetHandleOrThrow();
            var typedRun = (await typedA.StartOrGetAsync(new() { Value = 17 },
                StartIdempotencyKey.Create("cert-redeploy-" + Guid.NewGuid().ToString("N")), token)).GetHandleOrThrow();
            FacadeRedeployedInput.DropOnRead = true;
            var typedB = second.Services.GetRequiredService<IDagDefinitionRegistry>().Register(typedPlan).GetHandleOrThrow();
            Func<Task> changedDecoder = () => typedB.GetRunAsync(typedRun.RunId, token).AsTask();
            await changedDecoder.Should().ThrowAsync<DagRunInputContractMismatchException>();
            FacadeRedeployedInput.DropOnRead = false;
        }
        finally
        {
            FacadeRedeployedInput.DropOnRead = false;
            await StopFacadeHost(second); await StopFacadeHost(first);
        }
    }

    public static async Task FacadeCanonicalIntegrityAsync(Func<IServiceCollection> provider, string corruption, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90)); token = deadline.Token;
        var probe = new DagEventStoreProbe(); var observer = new DagIntegrityObserver(); var maps = 0;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(v => v).End(v => v.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("real-node"), child.Reference).MapInput(v => { Interlocked.Increment(ref maps); return v.RunInput; });
        var services = provider();
        // Install the real logger factory before the engine adds its standalone null fallback.
        services.AddLogging(); services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(observer);
        using var host = FacadeHost(services, probe, child);
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("cert-integrity-" + Guid.NewGuid().ToString("N"));
        var run = (await definition.StartOrGetAsync(17, key, token)).GetHandleOrThrow();
        await DagCanonicalIntegrityAssertions.RunAsync(host, probe, observer, definition, run, key, corruption, () => Volatile.Read(ref maps), token);
    }

    public static async Task FacadeFaultsAsync(Func<IServiceCollection> provider, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        token = deadline.Token;
        var probe = new DagEventStoreProbe();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<FacadeGate>().End(value => value.Value).Build();
        var authored = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        authored.Node(DagNodeId.Create("node"), child.Reference).MapInput(value => value.RunInput);
        var childGate = new FacadeGate(release.Task);
        var observer = new FacadeObserver();
        var registrations = provider();
        registrations.AddSingleton<IOutboxPumpObserver>(observer);
        using var host = FacadeHost(registrations, probe, child, childGate);
        await host.StartAsync(token);
        try
        {
            var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(authored.Build()).GetHandleOrThrow();
            var run = (await definition.StartOrGetAsync(17, StartIdempotencyKey.Create(Guid.NewGuid().ToString("N")), token)).GetHandleOrThrow();
            await childGate.Entered.Task.WaitAsync(token);
            var admitted = await FacadeReadAsync(probe.Inner, run.RunId.Value, token);
            var childCheckpoint = (await probe.Inner.LoadCheckpointAsync(admitted.Nodes[0].ChildInstanceId!, token)).Value;
            childCheckpoint.ParentInstanceId!.Value.Should().Be(run.RunId.Value);
            childCheckpoint.RootInstanceId!.Value.Should().Be(run.RunId.Value);
            probe.HoldNextSubscription(run.RunId.Value);
            var held = run.WaitForTerminalAsync(token).AsTask();
            await probe.Entered.Task.WaitAsync(token);
            await FacadeTransitionAsync(probe.Inner, run.RunId.Value, true, token);
            (await run.GetSnapshotAsync(token)).Status.Should().Be(DagRunStatus.Running);
            Func<Task> rejectCancel = () => run.RequestCancellationAsync(token).AsTask();
            await rejectCancel.Should().ThrowAsync<DagRunFaultedException>();
            Func<Task> rejectWait = () => run.WaitForTerminalAsync(token).AsTask();
            await rejectWait.Should().ThrowAsync<DagRunFaultedException>();
            await FacadeTransitionAsync(probe.Inner, run.RunId.Value, false, token);
            probe.Release.TrySetResult();
            await probe.NextSubscription.Task.WaitAsync(token);
            held.IsCompleted.Should().BeFalse("the authoritative recheck sees reconciliation");
            await FacadeTransitionAsync(probe.Inner, run.RunId.Value, true, token);
            Func<Task> notifiedWait = () => held;
            await notifiedWait.Should().ThrowAsync<DagRunFaultedException>("a still-committed fault wakes the registered waiter");
            await FacadeTransitionAsync(probe.Inner, run.RunId.Value, false, token);
            release.TrySetResult();
            try { (await run.WaitForTerminalAsync(token)).Status.Should().Be(DagRunStatus.Succeeded); }
            catch (OperationCanceledException)
            {
                using var inspect = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var facts = await FacadeReadAsync(probe.Inner, run.RunId.Value, inspect.Token);
                var childEvents = await probe.Inner.LoadTailAsync(new(facts.Nodes[0].ChildInstanceId!), StreamVersion.Empty, inspect.Token);
                throw new InvalidOperationException("Fault certification timed out after reconciliation: " +
                    JsonSerializer.Serialize(facts, Json) + "\nChild events: " + string.Join(",", childEvents.Select(item => item.GetType().Name)) +
                    "\nAppend failures: " + string.Join("\n", probe.AppendFailures) +
                    "\nPump observations: " + string.Join("\n", observer.Records));
            }
        }
        finally { probe.Release.TrySetResult(); release.TrySetResult(); await StopFacadeHost(host); }
    }

    public static Task FacadeTransitionAsync(IWorkflowEventStore events, Guid id, bool fault, CancellationToken token) =>
        OrcaCore.TestSupport.DagFacadeWire.FacadeTransitionAsync(events, id, fault, token);

    public static Task<DagRunSnapshotV1> FacadeReadAsync(IWorkflowEventStore events, Guid id, CancellationToken token) =>
        OrcaCore.TestSupport.DagFacadeWire.FacadeReadAsync(events, id, token);

    private sealed class FacadeGate(Task release) : IStep<int>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken token)
        { Entered.TrySetResult(); await release.WaitAsync(token); return new StepResult.Completed(); }
    }

    private sealed class FacadeObserver : IOutboxPumpObserver
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Records { get; } = new();
        public ValueTask OnDispatchCompletedAsync(OutboxDispatchObservation observation, CancellationToken token)
        {
            if (observation.Kind.StartsWith("dag-child-", StringComparison.Ordinal))
                Records.Enqueue($"{observation.Kind} attempt={observation.Attempt} {observation.Result} {observation.Exception}");
            return ValueTask.CompletedTask;
        }
        public ValueTask OnPumpCompletedAsync(OutboxPumpObservation observation, CancellationToken token) => ValueTask.CompletedTask;
    }

    private static IHost FacadeHost(IServiceCollection provider, DagEventStoreProbe? probe = null,
        DurableWorkflowDefinition<int, int>? child = null, FacadeGate? gate = null, int? dagLimit = 1)
    {
        if (probe is not null)
        {
            var port = provider.Last(descriptor => descriptor.ServiceType == typeof(IWorkflowEventStore));
            provider.AddSingleton<IWorkflowEventStore>(services =>
            { probe.Inner = (IWorkflowEventStore)port.ImplementationFactory!(services); return probe; });
        }
        var engine = provider.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions { PartitionId = ResourceGovernancePartitionId.Create("dag-facade-certification"), Pools = [] }
        });
        if (child is not null) engine.AddWorkflow(child);
        if (gate is not null) provider.AddSingleton(gate);
        if (dagLimit is { } limit) provider.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = limit });
        return Host.CreateDefaultBuilder().ConfigureServices(services =>
        { foreach (var descriptor in provider) services.Add(descriptor); }).Build();
    }

    private static async Task StopFacadeHost(IHost host)
    { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15)); await host.StopAsync(stop.Token); }
}
