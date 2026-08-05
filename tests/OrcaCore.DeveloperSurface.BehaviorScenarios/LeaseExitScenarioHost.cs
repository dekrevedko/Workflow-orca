using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static partial class LeaseExitScenarioHost
{
    [Phase0Scenario("obligation-state-machine", "3.11b")]
    public static async Task ObligationStateMachineClosesAtReleased(Phase0ScenarioContext context)
    {
        const string barrier = "obligation-state";
        context.ReleaseBarrier(barrier);
        using var fixture = await ActiveLeaseFixture.CreateAsync(context, barrier, "obligation-state");
        var diagnostics = fixture.Runtime.LeaseDiagnostics;
        var held = await context.ObserveAsync(
            _ => diagnostics.GetAsync(fixture.Token, CancellationToken.None));
        Phase0Assert.Satisfies(
            held,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.Held &&
                snapshot.Tickets.Count == 1,
            "The active lease did not materialize as one held obligation.");

        await TerminateAsync(fixture);
        var quarantined = await diagnostics.GetAsync(fixture.Token, CancellationToken.None);
        if (quarantined?.Status != DurableResourceLeaseObligationStatus.Quarantined)
        {
            throw new InvalidOperationException(
                "Terminal handoff did not move the held obligation to Quarantined.");
        }

        fixture.Gate.Release.TrySetResult();
        await fixture.Running;
        var recovery = fixture.Runtime.LeaseRecovery;
        var released = await recovery.ConfirmProtectedWorkStoppedAsync(
            fixture.Token,
            StopConfirmationId.Create("obligation-state-release"),
            CancellationToken.None);
        var terminal = await EnvelopeAsync(fixture.Store, fixture.InstanceId);
        if (released != ProtectedWorkStopConfirmationStatus.Released ||
            terminal.OwnedObligations.Single().LeasePhase != "Released")
        {
            throw new InvalidOperationException(
                "The quarantined obligation did not close at its successor-free Released tombstone.");
        }
    }

    [Phase0Scenario("release-before-parent-resume", "3.11b")]
    public static async Task ReleasePrecedesParentContinuation(Phase0ScenarioContext context)
    {
        const string barrier = "release-before-parent";
        context.ReleaseBarrier(barrier);
        using var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var probe = new LeaseProbe();
        var request = DatabaseRequest();
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(request, lease => lease.Then<CaptureLeaseStep>())
            .Then<CaptureParentCapacityStep>()
            .End()
            .Build();
        using var runtime = CreateRuntime(
            store,
            definition,
            context.Services.TimeProvider,
            PoolDefinitions(("database", 1, (TimeSpan?)null)),
            probe);
        var definitionHandle = runtime.Register(definition);
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            "release-before-parent",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var instance = await definitionHandle.GetInstanceAsync(
            started.InstanceId,
            CancellationToken.None);
        var state = await context.ObserveAsync(
            _ => instance.GetStateAsync<LeaseState>(CancellationToken.None));
        Phase0Assert.Satisfies(
            state,
            value => value is not null,
            "The post-release parent state was not available through the instance handle.");
        var terminal = await instance.GetSnapshotAsync(CancellationToken.None);
        if (terminal.Status != global::OrcaCore.WorkflowInstanceStatus.Completed)
        {
            throw new InvalidOperationException(
                "The post-release parent continuation did not commit completion.");
        }
        IDurableResourcePoolManagement management = runtime.ResourceManagement;
        var pool = await context.ObserveAsync(
            _ => management.GetAsync(
                ResourcePoolName.Create("database"),
                CancellationToken.None));
        Phase0Assert.Satisfies(
            pool,
            snapshot =>
                snapshot.ConfiguredCapacity == 1 &&
                snapshot.ReservedUnits == 0 &&
                snapshot.QueuedRequestCount == 0,
            "The exact ticket was not released before parent continuation.");
        if (probe.ParentCapacity != 1 || string.IsNullOrWhiteSpace(probe.ProtectionToken))
        {
            throw new InvalidOperationException(
                "The parent resumed before release or the leased body lacked its protection token.");
        }
    }

    [Phase0Scenario("cancellation-handoff-phases", "3.11b")]
    public static async Task CancellationHandoffPreservesPhaseTruth(Phase0ScenarioContext context)
    {
        const string barrier = "cancellation-handoff";
        context.ReleaseBarrier(barrier);
        using var fixture = await QueuedLeaseFixture.CreateAsync(context, barrier, "cancellation-handoff");
        var cancelled = await context.ObserveAsync(
            _ => fixture.Instance.RequestCancellationAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            cancelled,
            result => result == WorkflowCancellationRequestStatus.Requested,
            "Queued cancellation did not commit.");
        var token = LeaseProtectionToken.Parse(
            (await EnvelopeAsync(fixture.Store, fixture.InstanceId))
            .OwnedObligations.Single().ProtectionToken!);
        var diagnostics = fixture.Runtime.LeaseDiagnostics;
        var outstanding = await context.ObserveAsync(
            _ => diagnostics.GetAsync(token, CancellationToken.None));
        Phase0Assert.Satisfies(
            outstanding,
            snapshot => snapshot is null,
            "CancelledBeforeGrant was incorrectly reported as outstanding capacity.");

        var envelope = await EnvelopeAsync(fixture.Store, fixture.InstanceId);
        var obligation = envelope.OwnedObligations.Single();
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (obligation.LeasePhase != "CancelledBeforeGrant" ||
            obligation.LeaseTickets.Count != 0 ||
            pool.QueuedWaiters.Any(waiter => waiter.HolderInstanceId.Equals(fixture.InstanceId)))
        {
            throw new InvalidOperationException(
                "Queued cancellation erased its phase truth or retained provider ownership.");
        }
    }

    [Phase0Scenario("leased-redispatch-coordinate", "3.11b")]
    public static async Task LeasedAttemptCoordinateMatchesDurableObligation(
        Phase0ScenarioContext context)
    {
        const string barrier = "leased-coordinate";
        context.ReleaseBarrier(barrier);
        using var fixture = await ActiveLeaseFixture.CreateAsync(context, barrier, "leased-coordinate");
        var execution = fixture.Gate.Executions.Single();
        var operationId = context.Observe(_ => execution.OperationId);
        Phase0Assert.Satisfies(
            operationId,
            id => !string.IsNullOrWhiteSpace(id.Value),
            "The leased dispatch did not expose its durable operation identity.");
        var diagnostics = fixture.Runtime.LeaseDiagnostics;
        var snapshot = await context.ObserveAsync(
            _ => diagnostics.GetAsync(fixture.Token, CancellationToken.None));
        Phase0Assert.Satisfies(
            snapshot,
            value =>
                value is not null &&
                value.ProtectionToken.Equals(fixture.Token) &&
                value.Tickets.Count == 1 &&
                value.Tickets[0].ProviderGeneration > 0,
            "The leased attempt lost its stable token, ticket, units, or provider generation.");
        var envelope = await EnvelopeAsync(fixture.Store, fixture.InstanceId);
        var fiber = envelope.Fibers.Single(candidate => candidate.AttemptInFlight);
        if (fiber.LogicalOperationKey != execution.OperationId.Value ||
            fiber.RetryAttempt != execution.AttemptNumber)
        {
            throw new InvalidOperationException(
                "The dispatched leased attempt diverged from its persisted operation coordinate.");
        }

        fixture.Gate.Release.TrySetResult();
        await fixture.Running;
    }

    [Phase0Scenario("no-overlapping-leased-retry", "3.11b")]
    public static async Task TimedOutLeasedRetryWaitsForPhysicalReturn(
        Phase0ScenarioContext context)
    {
        const string barrier = "leased-no-overlap";
        context.ReleaseBarrier(barrier);
        var gate = new LeaseGate();
        using var store = new DurableScenarioProvider(context.Services.TimeProvider);
        Phase0Observation<DurableLeaseWorkflowBuilder<string, LeaseState>>? retry = null;
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(
                DatabaseRequest(),
                lease =>
                {
                    var step = lease.Then<BlockingLeaseStep>();
                    retry = context.Observe(_ => step.WithRetry(2));
                    step.WithStepTimeout(TimeSpan.FromMilliseconds(30));
                })
            .End()
            .Build();
        Phase0Assert.Satisfies(
            retry ?? throw new InvalidOperationException("Leased body was not authored."),
            selected => selected is not null,
            "WithRetry was unavailable on the leased business step.");
        using var runtime = CreateRuntime(
            store,
            definition,
            context.Services.TimeProvider,
            PoolDefinitions(("database", 1, (TimeSpan?)null)),
            new LeaseProbe(),
            gate);
        var handle = runtime.Register(definition);
        var running = handle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("leased-no-overlap"),
            CancellationToken.None).AsTask();
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
        await gate.TimeoutObserved.Task;
        await Task.Yield();
        if (gate.SecondStarted.Task.IsCompleted)
        {
            throw new InvalidOperationException(
                "A leased retry overlapped the still-running timed-out body.");
        }

        var instanceId = (await store.GetStartedAsync(
            "leased-no-overlap",
            CancellationToken.None)).Value.InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);
        gate.Release.TrySetResult();
        await gate.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await running;
        var diagnostics = runtime.LeaseDiagnostics;
        var quarantined = await context.ObserveAsync(
            _ => diagnostics.GetAsync(token, CancellationToken.None));
        Phase0Assert.Satisfies(
            quarantined,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.Quarantined,
            "The successful retry erased ambiguity instead of quarantining it.");
        if (gate.Attempts != 2)
        {
            throw new InvalidOperationException("The leased retry did not execute exactly twice.");
        }
    }

    [Phase0Scenario("successful-retry-retains-ambiguity", "3.11b")]
    public static async Task SuccessfulRetryRetainsAmbiguity(Phase0ScenarioContext context)
    {
        const string barrier = "successful-retry-ambiguity";
        context.ReleaseBarrier(barrier);
        using var fixture = await RunTimedOutRetryAsync(context, barrier, "successful-retry-ambiguity");
        var diagnostics = fixture.Runtime.LeaseDiagnostics;
        var observed = await context.ObserveAsync(
            _ => diagnostics.GetAsync(fixture.Token, CancellationToken.None));
        Phase0Assert.Satisfies(
            observed,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.Quarantined &&
                snapshot.Tickets.Count == 1,
            "A successful retry erased protected-work ambiguity or released its ticket.");
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (pool.AvailableCapacity != 0 || pool.HeldTickets.Count != 1)
        {
            throw new InvalidOperationException(
                "Ambiguous capacity was made available before truthful confirmation.");
        }
    }

    [Phase0Scenario("quarantine-before-progression", "3.11b")]
    public static async Task QuarantineCommitsBeforeTerminalProgression(
        Phase0ScenarioContext context)
    {
        const string barrier = "quarantine-before-terminal";
        context.ReleaseBarrier(barrier);
        using var fixture = await ActiveLeaseFixture.CreateAsync(
            context,
            barrier,
            "quarantine-before-terminal");
        await TerminateAsync(fixture);
        var diagnostics = fixture.Runtime.LeaseDiagnostics;
        var lease = await context.ObserveAsync(
            _ => diagnostics.GetAsync(fixture.Token, CancellationToken.None));
        Phase0Assert.Satisfies(
            lease,
            snapshot =>
                snapshot is not null &&
                snapshot.Status == DurableResourceLeaseObligationStatus.Quarantined,
            "Terminal progression occurred without an observable quarantine transfer.");
        var workflow = await context.ObserveAsync(
            _ => fixture.Instance.GetSnapshotAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            workflow,
            snapshot => snapshot.Status == global::OrcaCore.WorkflowInstanceStatus.Terminated,
            "The workflow did not commit the requested terminal status.");
        var pool = (await fixture.Pools.GetPoolAsync("database", CancellationToken.None)).Value;
        if (pool.AvailableCapacity != 0 || pool.HeldTickets.Count != 1)
        {
            throw new InvalidOperationException(
                "Terminal progression released quarantined capacity.");
        }

        fixture.Gate.Release.TrySetResult();
        await fixture.Running;
    }

    private static async Task<TimedOutRetryFixture> RunTimedOutRetryAsync(
        Phase0ScenarioContext context,
        string barrier,
        string key)
    {
        var gate = new LeaseGate();
        var store = new DurableScenarioProvider(context.Services.TimeProvider);
        var definition = Workflow.Durable<LeaseState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ =>
            {
                ConsumeBarrier(context, barrier);
                return new LeaseState();
            })
            .AcquireResources(
                DatabaseRequest(),
                lease => lease
                    .Then<BlockingLeaseStep>()
                    .WithRetry(2)
                    .WithStepTimeout(TimeSpan.FromMilliseconds(30)))
            .End()
            .Build();
        var runtime = CreateRuntime(
            store,
            definition,
            context.Services.TimeProvider,
            PoolDefinitions(("database", 1, (TimeSpan?)null)),
            new LeaseProbe(),
            gate);
        var pools = runtime.ResourcePools;
        var handle = runtime.Register(definition);
        var running = handle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create(key),
            CancellationToken.None).AsTask();
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var instanceId = (await store.GetStartedAsync(key, CancellationToken.None)).Value.InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);
        context.AdvanceTimeBy(TimeSpan.FromMilliseconds(100));
        await gate.TimeoutObserved.Task;
        await Task.Yield();
        gate.Release.TrySetResult();
        await gate.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await running;
        return new TimedOutRetryFixture(
            store,
            runtime,
            pools,
            token,
            instanceId,
            definition,
            key);
    }

    private static async Task TerminateAsync(ActiveLeaseFixture fixture)
    {
        var result = await fixture.Instance.TerminateAsync(CancellationToken.None);
        if (result != WorkflowTerminationStatus.Terminated)
        {
            throw new InvalidOperationException("Termination did not commit.");
        }
    }

    private static ResourceLeaseRequest DatabaseRequest() =>
        ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));

    private static IReadOnlyList<DurableResourcePoolDefinition> PoolDefinitions(
        params (string Name, int Capacity, TimeSpan? ReviewAfter)[] definitions)
        => definitions
            .Select(definition => DurableResourcePoolDefinition.Create(
                ResourcePoolName.Create(definition.Name),
                definition.Capacity,
                definition.ReviewAfter ?? TimeSpan.FromMinutes(5)))
            .ToArray();

    private static DurableScenarioRuntime CreateRuntime(
        DurableScenarioProvider store,
        DurableWorkflowDefinition<string> definition,
        TimeProvider timeProvider,
        IReadOnlyList<DurableResourcePoolDefinition> pools,
        LeaseProbe probe,
        LeaseGate? gate = null,
        Func<IResourcePoolStore, IResourcePoolStore>? decorateResourcePools = null) =>
        DurableScenarioRuntime.Create(
            store,
            timeProvider,
            resourcePools: pools,
            configureServices: services =>
            {
                services.AddSingleton(probe);
                services.AddTransient<CaptureLeaseStep>();
                services.AddTransient<CaptureParentCapacityStep>();
                services.AddTransient<NoOpStep>();
                if (gate is not null)
                {
                    services.AddSingleton(gate);
                    services.AddTransient<BlockingLeaseStep>();
                }
            },
            partition: $"lease-exit-{definition.DefinitionId.Value:N}",
            decorateResourcePools: decorateResourcePools);

    private static async Task<DurableExecutionEnvelopeV2> EnvelopeAsync(
        IWorkflowEventStore store,
        InstanceId instanceId)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, CancellationToken.None);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private static void ConsumeBarrier(Phase0ScenarioContext context, string name) =>
        context.Services.Barrier.ReachAsync(name).GetAwaiter().GetResult();

    public sealed class LeaseState;

    private sealed class LeaseProbe
    {
        internal string? ProtectionToken;
        internal int ParentCapacity;
    }

    private sealed class LeaseGate
    {
        internal int Attempts;
        internal List<StepExecutionContext> Executions { get; } = [];
        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource TimeoutObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class CaptureLeaseStep(LeaseProbe probe) : IStep<LeaseState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken)
        {
            probe.ProtectionToken = context.ResourceLease?.ProtectionToken.Value;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureParentCapacityStep(
        IResourcePoolStore pools,
        LeaseProbe probe) : IStep<LeaseState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken)
        {
            probe.ParentCapacity = (await pools.GetPoolAsync(
                "database",
                cancellationToken)).Value.AvailableCapacity;
            return new StepResult.Completed();
        }
    }

    private sealed class BlockingLeaseStep(LeaseGate gate) : IStep<LeaseState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken)
        {
            using var timeoutRegistration = cancellationToken.Register(
                () => gate.TimeoutObserved.TrySetResult());
            var attempt = Interlocked.Increment(ref gate.Attempts);
            lock (gate.Executions)
            {
                gate.Executions.Add(context.Execution);
            }

            if (attempt == 1)
            {
                gate.FirstStarted.TrySetResult();
                await gate.Release.Task;
            }
            else
            {
                gate.SecondStarted.TrySetResult();
            }

            return new StepResult.Completed();
        }
    }

    private sealed class NoOpStep : IStep<LeaseState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<LeaseState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record TimedOutRetryFixture(
        DurableScenarioProvider Store,
        DurableScenarioRuntime Runtime,
        IResourcePoolStore Pools,
        LeaseProtectionToken Token,
        InstanceId InstanceId,
        DurableWorkflowDefinition<string> Definition,
        string Key) : IDisposable
    {
        public void Dispose()
        {
            Runtime.Dispose();
            Store.Dispose();
        }
    }

    private sealed class ActiveLeaseFixture : IDisposable
    {
        private ActiveLeaseFixture(
            DurableScenarioProvider store,
            DurableScenarioRuntime runtime,
            IResourcePoolStore pools,
            LeaseGate gate,
            Task<WorkflowStartResult<WorkflowInstanceHandle>> running,
            InstanceId instanceId,
            LeaseProtectionToken token,
            WorkflowInstanceHandle instance)
        {
            Store = store;
            Runtime = runtime;
            Pools = pools;
            Gate = gate;
            Running = running;
            InstanceId = instanceId;
            Token = token;
            Instance = instance;
        }

        internal DurableScenarioProvider Store { get; }
        internal DurableScenarioRuntime Runtime { get; }
        internal IResourcePoolStore Pools { get; }
        internal LeaseGate Gate { get; }
        internal Task<WorkflowStartResult<WorkflowInstanceHandle>> Running { get; }
        internal InstanceId InstanceId { get; }
        internal LeaseProtectionToken Token { get; }
        internal WorkflowInstanceHandle Instance { get; }

        internal static async Task<ActiveLeaseFixture> CreateAsync(
            Phase0ScenarioContext context,
            string barrier,
            string key)
        {
            var store = new DurableScenarioProvider(context.Services.TimeProvider);
            var gate = new LeaseGate();
            var definition = Workflow.Durable<LeaseState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ =>
                {
                    ConsumeBarrier(context, barrier);
                    return new LeaseState();
                })
                .AcquireResources(
                    DatabaseRequest(),
                    lease => lease.Then<BlockingLeaseStep>())
                .End()
                .Build();
            var runtime = CreateRuntime(
                store,
                definition,
                context.Services.TimeProvider,
                PoolDefinitions(("database", 1, (TimeSpan?)null)),
                new LeaseProbe(),
                gate);
            var pools = runtime.ResourcePools;
            var definitionHandle = runtime.Register(definition);
            var running = definitionHandle.StartOrGetAsync(
                "input",
                StartIdempotencyKey.Create(key),
                CancellationToken.None).AsTask();
            await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var instanceId = (await store.GetStartedAsync(key, CancellationToken.None)).Value.InstanceId;
            var envelope = await EnvelopeAsync(store, instanceId);
            var token = LeaseProtectionToken.Parse(
                envelope.OwnedObligations.Single(
                    obligation => obligation.ProtectionToken is not null).ProtectionToken!);
            var instance = await definitionHandle.GetInstanceAsync(
                instanceId,
                CancellationToken.None);
            return new ActiveLeaseFixture(
                store,
                runtime,
                pools,
                gate,
                running,
                instanceId,
                token,
                instance);
        }

        public void Dispose()
        {
            Runtime.Dispose();
            Store.Dispose();
        }
    }

    private sealed class QueuedLeaseFixture : IDisposable
    {
        private QueuedLeaseFixture(
            DurableScenarioProvider store,
            DurableScenarioRuntime runtime,
            IResourcePoolStore pools,
            InstanceId instanceId,
            WorkflowInstanceHandle instance)
        {
            Store = store;
            Runtime = runtime;
            Pools = pools;
            InstanceId = instanceId;
            Instance = instance;
        }

        internal DurableScenarioProvider Store { get; }
        internal DurableScenarioRuntime Runtime { get; }
        internal IResourcePoolStore Pools { get; }
        internal InstanceId InstanceId { get; }
        internal WorkflowInstanceHandle Instance { get; }

        internal static async Task<QueuedLeaseFixture> CreateAsync(
            Phase0ScenarioContext context,
            string barrier,
            string key)
        {
            var store = new DurableScenarioProvider(context.Services.TimeProvider);
            var definition = Workflow.Durable<LeaseState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ =>
                {
                    ConsumeBarrier(context, barrier);
                    return new LeaseState();
                })
                .AcquireResources(DatabaseRequest(), lease => lease.Then<NoOpStep>())
                .End()
                .Build();
            var runtime = CreateRuntime(
                store,
                definition,
                context.Services.TimeProvider,
                PoolDefinitions(("database", 1, (TimeSpan?)null)),
                new LeaseProbe());
            var pools = runtime.ResourcePools;
            var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
            _ = await pools.AcquireAsync(
                new ResourcePoolAcquireRequest(
                    blocker,
                    "external",
                    [new ResourcePoolRequirement("database", 1)],
                    context.Services.TimeProvider.GetUtcNow(),
                    ExpiresAt: null),
                CancellationToken.None);
            var definitionHandle = runtime.Register(definition);
            var started = await runtime.StartOrGetAsync<string, LeaseState>(
                key,
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                CancellationToken.None);
            var instance = await definitionHandle.GetInstanceAsync(
                started.InstanceId,
                CancellationToken.None);
            return new QueuedLeaseFixture(store, runtime, pools, started.InstanceId, instance);
        }

        public void Dispose()
        {
            Runtime.Dispose();
            Store.Dispose();
        }
    }
}
