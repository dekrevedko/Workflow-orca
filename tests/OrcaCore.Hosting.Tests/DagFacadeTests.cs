using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Durable.Hosting.Dag;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

// Public facade exercised against the real bridge, scheduler, pumps and in-memory provider.
public sealed partial class DagFacadeTests(ITestOutputHelper output)
{
    private static CancellationTokenSource Deadline(int seconds = 60)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.CancelAfter(TimeSpan.FromSeconds(seconds));
        return source;
    }

    [Fact]
    public async Task CreateReplayReopen_AreConsistentUnderOneBinding()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value + 1).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var node = dag.Node(DagNodeId.Create("only"), child.Reference).MapInput(input => input.RunInput);
        var plan = dag.Build();
        using var host = CreateHost(4, services => { }, child);
        await host.StartAsync(token);
        try
        {
            var registry = host.Services.GetRequiredService<IDagDefinitionRegistry>();
            var definition = registry.Register(plan).GetHandleOrThrow();
            registry.Register(plan).Should().BeOfType<DagRegistrationResult<int>.Registered>("registration is idempotent");

            var start = await definition.StartOrGetAsync(41, StartIdempotencyKey.Create("facade-create"), token);
            start.Should().BeOfType<DagStartResult.Accepted>().Which.WasExisting.Should().BeFalse();
            var run = start.GetHandleOrThrow();

            var replay = await definition.StartOrGetAsync(41, StartIdempotencyKey.Create("facade-create"), token);
            replay.Should().BeOfType<DagStartResult.Accepted>().Which.WasExisting.Should().BeTrue();
            replay.GetHandleOrThrow().RunId.Should().Be(run.RunId, "replay returns the key owner's run id");

            (await definition.StartOrGetAsync(7, StartIdempotencyKey.Create("facade-create"), token))
                .Should().BeOfType<DagStartResult.Conflict>("changed input under one key is an idempotency conflict");

            var reopened = await definition.GetRunAsync(run.RunId, token);
            var terminal = await reopened.WaitForTerminalAsync(token);
            terminal.Status.Should().Be(DagRunStatus.Succeeded);
            terminal.Nodes.Single().Status.Should().Be(DagNodeStatus.Succeeded);
            terminal.Nodes.Single().MappedInputFingerprint.Should().NotBeNull();
            (await run.GetOutputAsync(node, token)).Should().BeOfType<DagNodeOutputResult<int>.Available>()
                .Which.Output.Should().Be(42);

            Func<Task> missing = () => definition.GetRunAsync(DagRunId.Parse(Guid.NewGuid().ToString("D")), token).AsTask();
            await missing.Should().ThrowAsync<DagRunNotFoundException>();
        }
        finally { await StopAsync(host); }
    }

    public sealed class Lossy
    {
        public int Value { get; private set; }
        public static Lossy Create(int value) => new() { Value = value };
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task LossyMappedInput_FailsTheNodeBeforeAChildStarts()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Lossy>(input => input.Value).End(value => value.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("child"), child.Reference).MapInput(input => Lossy.Create(input.RunInput));
        using var host = CreateHost(1, _ => { });
        host.Services.GetRequiredService<IWorkflowDefinitionRegistry>().Register(child).GetHandleOrThrow();
        await host.StartAsync(token);
        try
        {
            var handle = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
            var run = (await handle.StartOrGetAsync(17, StartIdempotencyKey.Create("lossy-map"), token)).GetHandleOrThrow();
            var node = (await run.WaitForTerminalAsync(token)).Nodes.Single();
            node.Failure!.Code.Should().Be("DAG_INPUT_MAPPING_INVALID");
            node.ChildInstanceId.Should().BeNull();
            node.MappedInputFingerprint.Should().BeNull();
        }
        finally { await StopAsync(host); }
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task LossyRunInput_IsRejectedBeforeAnyRunExists()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<Lossy>(DefinitionId.New(), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("only"), child.Reference).MapInput(input => input.RunInput.Value);
        using var host = CreateHost(4, services => { }, child);
        await host.StartAsync(token);
        try
        {
            var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
            var error = await Record.ExceptionAsync(() =>
                definition.StartOrGetAsync(Lossy.Create(17), StartIdempotencyKey.Create("facade-lossy"), token).AsTask());
            output.WriteLine($"lossy start: {error?.GetType().Name}: {error?.Message}");
            error.Should().BeOfType<NotSupportedException>();
            (await host.Services.GetRequiredService<IWorkflowEventStore>()
                .GetDagRunByStartKeyAsync(new DagRunStartKeyV1("facade-lossy"), token)).HasValue.Should().BeFalse();
        }
        finally { await StopAsync(host); }
    }

    // Same CLR type, same shape, same contract key. The flag makes the type read stored bytes
    // differently, standing in for a redeployed DTO that kept its name.
    public sealed class Redeployable
    {
        public static volatile bool DropOnRead;
        private int value;
        public int Value { get => value; set { if (!DropOnRead) this.value = value; } }
    }

    [Fact]
    [Trait("AC", "AC-328")]
    public async Task StoredInputThatNoLongerRoundTrips_IsSkippedNotFailed_AndACompatibleHostFinishesIt()
    {
        using var deadline = Deadline(90);
        var token = deadline.Token;
        Redeployable.DropOnRead = false;
        var gated = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).Then<Gate>().End(state => state.Value).Build();
        var plain = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).End(state => state.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<Redeployable>(DefinitionId.New(), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("first"), gated.Reference).MapInput(input => input.RunInput.Value);
        dag.Node(DagNodeId.Create("second"), plain.Reference).MapInput(input => input.RunInput.Value);
        var plan = dag.Build();

        var gateA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostA = CreateHost(1, services => services.AddSingleton(new Gate(gateA.Task)), gated, plain);
        await hostA.StartAsync(token);
        var shared = hostA.Services.GetRequiredService<IWorkflowEventStore>();
        var definitionA = hostA.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var run = (await definitionA.StartOrGetAsync(new Redeployable { Value = 17 },
            StartIdempotencyKey.Create("facade-redeploy"), token)).GetHandleOrThrow();
        await UntilAsync(run, snapshot => snapshot.Nodes[0].Status == DagNodeStatus.Running, token);
        (await run.GetSnapshotAsync(token)).Nodes[1].Status.Should().Be(DagNodeStatus.Ready, "one slot is taken");
        await StopAsync(hostA, dispose: false);

        // "Redeploy": a new host over the same storage whose type now reads the bytes differently.
        Redeployable.DropOnRead = true;
        var gateB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostB = CreateHost(1, services =>
        {
            services.AddSingleton(shared.GetType(), shared);
            services.AddSingleton(new Gate(gateB.Task));
        }, gated, plain);
        await hostB.StartAsync(token);
        var definitionB = hostB.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var reopen = await Record.ExceptionAsync(() => definitionB.GetRunAsync(run.RunId, token).AsTask());
        output.WriteLine($"reopen on incompatible host: {reopen?.GetType().Name}");
        reopen.Should().BeOfType<DagRunInputContractMismatchException>().Which.RunId.Should().Be(run.RunId);

        // The role-independent child pump still joins the first child; the scheduler must not map node 1.
        gateB.SetResult();
        var bridgeB = hostB.Services.GetRequiredService<DurableDagRunBridge>();
        var facts = (await bridgeB.ReadAsync(run.RunId.Value, token))!;
        while (facts.Nodes[0].State != DagNodeStateV1.Succeeded)
            facts = await bridgeB.WaitForChangeAsync(run.RunId.Value, facts.StreamVersion, token);
        await Task.Delay(TimeSpan.FromSeconds(10), TimeProvider.System, token); // more than two discovery passes
        facts = (await bridgeB.ReadAsync(run.RunId.Value, token))!;
        output.WriteLine($"after incompatible host: run {facts.State}, node1 {facts.Nodes[1].State} {facts.Nodes[1].FailureCode}");
        facts.State.Should().Be(DagRunStateV1.Running, "an incompatible host must not fault or fail the run");
        facts.Nodes[1].State.Should().Be(DagNodeStateV1.Ready, "an incompatible host must not record a mapping failure");
        await StopAsync(hostB, dispose: false);

        // A compatible host over the same storage reopens and finishes the same run.
        Redeployable.DropOnRead = false;
        var hostC = CreateHost(1, services =>
        {
            services.AddSingleton(shared.GetType(), shared);
            services.AddSingleton(new Gate(Task.CompletedTask));
        }, gated, plain);
        await hostC.StartAsync(token);
        try
        {
            var definitionC = hostC.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
            var terminal = await (await definitionC.GetRunAsync(run.RunId, token)).WaitForTerminalAsync(token);
            terminal.Status.Should().Be(DagRunStatus.Succeeded);
        }
        finally
        {
            await StopAsync(hostC);
            hostB.Dispose();
            hostA.Dispose();
        }
    }

    [Fact]
    public async Task Waiter_HeldAcrossFaultAndReconciliation_ContinuesWithoutException()
    {
        using var deadline = Deadline(90);
        var token = deadline.Token;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new DagEventStoreProbe();
        var (host, run) = await StartGatedRunAsync(gate, "facade-held", token, probe);
        probe.HoldNextSubscription(run.RunId.Value);
        try
        {
            var waiter = run.WaitForTerminalAsync(token).AsTask();
            await probe.Entered.Task.WaitAsync(token);
            await FaultAsync(host, run.RunId.Value, token);
            await ReconcileAsync(host, run.RunId.Value, token);
            probe.Release.TrySetResult();
            await probe.NextSubscription.Task.WaitAsync(token);
            output.WriteLine($"held waiter after fault+reconcile: {waiter.Status}");
            waiter.IsCompleted.Should().BeFalse("the recheck observes the reconciled checkpoint, not the earlier fault");
            gate.SetResult();
            (await waiter).Status.Should().Be(DagRunStatus.Succeeded);
        }
        finally { gate.TrySetResult(); await StopAsync(host); }
    }

    [Fact]
    public async Task FaultedRun_ThrowsFromWaitAndCancel_KeepsPreFaultStatus_ThenRecovers()
    {
        using var deadline = Deadline(90);
        var token = deadline.Token;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new DagEventStoreProbe();
        var (host, run) = await StartGatedRunAsync(gate, "facade-fault", token, probe);
        probe.HoldNextSubscription(run.RunId.Value);
        try
        {
            var waiter = run.WaitForTerminalAsync(token).AsTask();
            await probe.Entered.Task.WaitAsync(token);
            await FaultAsync(host, run.RunId.Value, token);
            probe.Release.TrySetResult();
            var woke = await Record.ExceptionAsync(() => waiter);
            woke.Should().BeOfType<DagRunFaultedException>("a fault committed after registration wakes the waiter");
            (await run.GetSnapshotAsync(token)).Status.Should().Be(DagRunStatus.Running);
            var early = await Record.ExceptionAsync(() => run.WaitForTerminalAsync(token).AsTask());
            early.Should().BeOfType<DagRunFaultedException>("a fault already committed fails the first recheck");
            var cancel = await Record.ExceptionAsync(() => run.RequestCancellationAsync(token).AsTask());
            cancel.Should().BeOfType<DagRunFaultedException>("no cancellation barrier can commit on a faulted run");

            await ReconcileAsync(host, run.RunId.Value, token);
            (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.Requested);
            await FaultAsync(host, run.RunId.Value, token);
            (await run.GetSnapshotAsync(token)).Status.Should().Be(DagRunStatus.CancellationRequested,
                "the pre-fault status is derived from node states");
            (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.AlreadyRequested);
            await ReconcileAsync(host, run.RunId.Value, token);
            gate.SetResult();
            (await run.WaitForTerminalAsync(token)).Status.Should().Be(DagRunStatus.Cancelled);
        }
        finally { gate.TrySetResult(); await StopAsync(host); }
    }

    private static async Task<(IHost Host, DagRunHandle Run)> StartGatedRunAsync(
        TaskCompletionSource gate, string key, CancellationToken token, DagEventStoreProbe? probe = null)
    {
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => input).Then<Gate>().End(state => state.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("only"), child.Reference).MapInput(input => input.RunInput);
        var host = CreateHost(4, services =>
        {
            services.AddSingleton(new Gate(gate.Task));
            if (probe is not null) Decorate(services, probe);
        }, child);
        await host.StartAsync(token);
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
        var run = (await definition.StartOrGetAsync(1, StartIdempotencyKey.Create(key), token)).GetHandleOrThrow();
        await UntilAsync(run, snapshot => snapshot.Nodes[0].Status == DagNodeStatus.Running, token);
        return (host, run);
    }

    private static async Task FaultAsync(IHost host, Guid runId, CancellationToken token)
    {
        var bridge = host.Services.GetRequiredService<IDurableDagRunBridgeV1>();
        var request = new DurableDagTurnRequestV1(runId, Guid.NewGuid(), DurableDagTurnPurposeV1.IntegrityReconciliation);
        var grant = await GrantAsync(bridge, request, token);
        (await bridge.RecordIntegrityFaultAsync(runId, "DAG_PROTOCOL_INVALID", grant.Fence, grant.Version, token))
            .Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        await bridge.EndTurnAsync(grant, token);
    }

    private static async Task ReconcileAsync(IHost host, Guid runId, CancellationToken token)
    {
        var bridge = host.Services.GetRequiredService<IDurableDagRunBridgeV1>();
        var request = new DurableDagTurnRequestV1(runId, Guid.NewGuid(), DurableDagTurnPurposeV1.IntegrityReconciliation);
        var grant = await GrantAsync(bridge, request, token);
        (await bridge.ReconcilePoisonAsync(runId, Guid.NewGuid(), grant.Fence, grant.Version, token))
            .Outcome.Should().Be(DurableDagMutationOutcomeV1.Committed);
        await bridge.EndTurnAsync(grant, token);
    }

    private static async Task<DurableDagTurnV1> GrantAsync(IDurableDagRunBridgeV1 bridge, DurableDagTurnRequestV1 request,
        CancellationToken token)
    {
        while (true)
        {
            var result = await bridge.BeginTurnAsync(request, token);
            if (result.Turn is { } turn) return turn;
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
        }
    }

    private static async Task UntilAsync(DagRunHandle run, Func<DagRunSnapshot, bool> condition, CancellationToken token)
    {
        while (!condition(await run.GetSnapshotAsync(token)))
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
    }

    private sealed class Gate(Task release) : IStep<int>
    {
        public async ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
        {
            await release.WaitAsync(cancellationToken);
            return new StepResult.Completed();
        }
    }

    private static IHost CreateHost(int limit, Action<IServiceCollection> configure,
        params DurableWorkflowDefinition<int, int>[] children) =>
        Host.CreateDefaultBuilder().ConfigureServices(services =>
        {
            services.AddOrcaCoreInMemoryDurableProvider();
            configure(services);
            var engine = services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = ResourceGovernancePartitionId.Create("dag-facade-tests"), Pools = []
                }
            });
            foreach (var child in children) engine.AddWorkflow(child);
            services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = limit });
        }).Build();

    private static void Decorate(IServiceCollection services, DagEventStoreProbe probe)
    {
        var port = services.Last(descriptor => descriptor.ServiceType == typeof(IWorkflowEventStore));
        services.AddSingleton<IWorkflowEventStore>(provider =>
        {
            probe.Inner = (IWorkflowEventStore)port.ImplementationFactory!(provider);
            return probe;
        });
    }

    private static async Task StopAsync(IHost host, bool dispose = true)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await host.StopAsync(stop.Token);
        if (dispose) host.Dispose();
    }
}
