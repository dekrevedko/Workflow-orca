using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static partial class LeaseExitScenarioHost
{
    [Phase0Scenario("obligation-state-machine", "3.11b")]
    public static async Task ObligationStateMachineClosesAtReleased(Phase0ScenarioContext context)
    {
        const string barrier = "obligation-state";
        context.ReleaseBarrier(barrier);
        var fixture = await ActiveLeaseFixture.CreateAsync(context, barrier, "obligation-state");
        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
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
        var recovery = CreateRecovery(fixture.Processor, fixture.Store);
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
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
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
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, probe));
        var started = await runtime.StartOrGetAsync<string, LeaseState>(
            "release-before-parent",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);

        var definitionHandle = DurableFacadeScenarioAdapter.Register(
            runtime,
            store,
            store,
            new DurableManagement(store),
            context.Services.TimeProvider,
            definition,
            [ResourcePoolName.Create("database")]);
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
        IDurableResourcePoolManagement management = new ResourcePoolManagementView(pools);
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

    private sealed class ResourcePoolManagementView(
        IResourcePoolStore store) : IDurableResourcePoolManagement
    {
        public async ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            var pools = await store.ListPoolsAsync(cancellationToken).ConfigureAwait(false);
            return pools.Select(ToSnapshot).ToArray();
        }

        public async ValueTask<DurableResourcePoolSnapshot> GetAsync(
            ResourcePoolName pool,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pool);
            var found = await store.GetPoolAsync(pool.Value, cancellationToken).ConfigureAwait(false);
            if (!found.HasValue)
            {
                throw ResourcePoolNotConfiguredException.For([pool]);
            }

            return ToSnapshot(found.Value);
        }

        public ValueTask<DurableResourcePoolResizeResult> ResizeAsync(
            ResourcePoolName pool,
            int capacity,
            ResourcePoolOperationId operationId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static DurableResourcePoolSnapshot ToSnapshot(ResourcePoolSnapshot pool)
        {
            var reserved = pool.HeldTickets.Sum(ticket => ticket.Count);
            return new DurableResourcePoolSnapshot(
                ResourcePoolName.Create(pool.Name),
                pool.Capacity,
                reserved,
                Math.Max(0, reserved - pool.Capacity),
                pool.QueuedWaiters.Count,
                pool.HeldTickets
                    .Where(ticket => ticket.ReviewDeadline is not null)
                    .Select(ticket => ticket.ReviewDeadline)
                    .Min());
        }
    }

    [Phase0Scenario("cancellation-handoff-phases", "3.11b")]
    public static async Task CancellationHandoffPreservesPhaseTruth(Phase0ScenarioContext context)
    {
        const string barrier = "cancellation-handoff";
        context.ReleaseBarrier(barrier);
        var fixture = await QueuedLeaseFixture.CreateAsync(context, barrier, "cancellation-handoff");
        var cancelled = await context.ObserveAsync(
            _ => fixture.Instance.RequestCancellationAsync(CancellationToken.None));
        Phase0Assert.Satisfies(
            cancelled,
            result => result == WorkflowCancellationRequestStatus.Requested,
            "Queued cancellation did not commit.");
        var token = LeaseProtectionToken.Parse(
            (await EnvelopeAsync(fixture.Store, fixture.InstanceId))
            .OwnedObligations.Single().ProtectionToken!);
        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
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
        var fixture = await ActiveLeaseFixture.CreateAsync(context, barrier, "leased-coordinate");
        var execution = fixture.Gate.Executions.Single();
        var operationId = context.Observe(_ => execution.OperationId);
        Phase0Assert.Satisfies(
            operationId,
            id => !string.IsNullOrWhiteSpace(id.Value),
            "The leased dispatch did not expose its durable operation identity.");
        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
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
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
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
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), gate),
            processor);
        var running = runtime.StartOrGetAsync<string, LeaseState>(
            "leased-no-overlap",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, CancellationToken.None);
        if (gate.SecondStarted.Task.IsCompleted)
        {
            throw new InvalidOperationException(
                "A leased retry overlapped the still-running timed-out body.");
        }

        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            CancellationToken.None)).Single().InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);
        gate.Release.TrySetResult();
        await gate.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await running;
        var diagnostics = CreateDiagnostics(processor, store);
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
        var fixture = await RunTimedOutRetryAsync(context, barrier, "successful-retry-ambiguity");
        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
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
        var fixture = await ActiveLeaseFixture.CreateAsync(
            context,
            barrier,
            "quarantine-before-terminal");
        await TerminateAsync(fixture);
        var diagnostics = CreateDiagnostics(fixture.Processor, fixture.Store);
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
        var store = new InMemoryWorkflowProvider();
        var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
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
        var processor = new DurableCommandProcessor(store, pools);
        var runtime = CreateRuntime(
            store,
            pools,
            definition,
            context.Services.TimeProvider,
            new LeaseServices(pools, new LeaseProbe(), gate),
            processor);
        var running = runtime.StartOrGetAsync<string, LeaseState>(
            key,
            definition.DefinitionId,
            definition.DefinitionVersion,
            "input",
            CancellationToken.None);
        await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var instanceId = (await store.ListAsync(
            new WorkflowProjectionQuery(),
            CancellationToken.None)).Single().InstanceId;
        var active = await EnvelopeAsync(store, instanceId);
        var token = LeaseProtectionToken.Parse(
            active.OwnedObligations.Single(obligation => obligation.ProtectionToken is not null)
                .ProtectionToken!);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, CancellationToken.None);
        gate.Release.TrySetResult();
        await gate.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await running;
        return new TimedOutRetryFixture(
            store,
            pools,
            processor,
            token,
            instanceId,
            definition,
            key);
    }

    private static async Task TerminateAsync(ActiveLeaseFixture fixture)
    {
        var result = await fixture.Processor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = fixture.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow
            },
            CancellationToken.None);
        if (result.Outcome != DurableCommandOutcome.Committed)
        {
            throw new InvalidOperationException("Termination did not commit.");
        }
    }

    private static IDurableResourceLeaseDiagnostics CreateDiagnostics(
        DurableCommandProcessor processor,
        IWorkflowProjectionStore projections)
    {
        var runtime = CreateInternal(
            typeof(DurableWorkflowRuntime).Assembly,
            "OrcaCore.Engine.Durable.Driver.DurableResourceLeaseDiagnostics",
            processor,
            projections);
        return (IDurableResourceLeaseDiagnostics)CreateInternal(
            typeof(IDurableResourceLeaseDiagnostics).Assembly,
            "OrcaCore.Hosting.ResourceLeases.HostedDurableResourceLeaseDiagnostics",
            runtime);
    }

    private static IDurableResourceLeaseRecovery CreateRecovery(
        DurableCommandProcessor processor,
        IWorkflowProjectionStore projections)
    {
        var runtime = CreateInternal(
            typeof(DurableWorkflowRuntime).Assembly,
            "OrcaCore.Engine.Durable.Driver.DurableResourceLeaseRecovery",
            processor,
            projections,
            TimeProvider.System);
        return (IDurableResourceLeaseRecovery)CreateInternal(
            typeof(IDurableResourceLeaseRecovery).Assembly,
            "OrcaCore.Hosting.ResourceLeases.HostedDurableResourceLeaseRecovery",
            runtime);
    }

    private static object CreateInternal(Assembly assembly, string typeName, params object[] arguments)
    {
        var type = assembly.GetType(typeName, throwOnError: true)!;
        return Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: arguments,
            culture: null)!;
    }

    private static ResourceLeaseRequest DatabaseRequest() =>
        ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));

    private static async Task<InMemoryResourcePoolStore> CreatePoolsAsync(
        params (string Name, int Capacity, TimeSpan? ReviewAfter)[] definitions)
    {
        var pools = new InMemoryResourcePoolStore();
        foreach (var definition in definitions)
        {
            await pools.UpsertPoolAsync(
                new ResourcePoolDefinition(
                    definition.Name,
                    definition.Capacity,
                    definition.ReviewAfter),
                CancellationToken.None);
        }

        return pools;
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        IResourcePoolStore pools,
        DurableWorkflowDefinition<string> definition,
        TimeProvider timeProvider,
        IServiceProvider services,
        DurableCommandProcessor? processor = null)
    {
        var runtime = new DurableWorkflowRuntime(
            processor ?? new DurableCommandProcessor(store, pools),
            new DurableDefinitionRegistry(services),
            timeProvider,
            DurableDriverBudget.Default);
        runtime.RegisterDefinition(RuntimeDefinition<LeaseState>(definition));
        return runtime;
    }

    private static WorkflowDefinition<TState> RuntimeDefinition<TState>(object publicDefinition) =>
        (WorkflowDefinition<TState>)publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;

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

    private sealed class LeaseServices(
        IResourcePoolStore pools,
        LeaseProbe probe,
        LeaseGate? gate = null) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(CaptureLeaseStep))
            {
                return new CaptureLeaseStep(probe);
            }

            if (serviceType == typeof(CaptureParentCapacityStep))
            {
                return new CaptureParentCapacityStep(pools, probe);
            }

            if (serviceType == typeof(BlockingLeaseStep) && gate is not null)
            {
                return new BlockingLeaseStep(gate);
            }

            if (serviceType == typeof(NoOpStep))
            {
                return new NoOpStep();
            }

            return null;
        }
    }

    private sealed record TimedOutRetryFixture(
        InMemoryWorkflowProvider Store,
        InMemoryResourcePoolStore Pools,
        DurableCommandProcessor Processor,
        LeaseProtectionToken Token,
        InstanceId InstanceId,
        DurableWorkflowDefinition<string> Definition,
        string Key);

    private sealed class ActiveLeaseFixture
    {
        private ActiveLeaseFixture(
            InMemoryWorkflowProvider store,
            InMemoryResourcePoolStore pools,
            DurableCommandProcessor processor,
            LeaseGate gate,
            Task<DurableWorkflowStartResult> running,
            InstanceId instanceId,
            LeaseProtectionToken token,
            WorkflowInstanceHandle instance)
        {
            Store = store;
            Pools = pools;
            Processor = processor;
            Gate = gate;
            Running = running;
            InstanceId = instanceId;
            Token = token;
            Instance = instance;
        }

        internal InMemoryWorkflowProvider Store { get; }
        internal InMemoryResourcePoolStore Pools { get; }
        internal DurableCommandProcessor Processor { get; }
        internal LeaseGate Gate { get; }
        internal Task<DurableWorkflowStartResult> Running { get; }
        internal InstanceId InstanceId { get; }
        internal LeaseProtectionToken Token { get; }
        internal WorkflowInstanceHandle Instance { get; }

        internal static async Task<ActiveLeaseFixture> CreateAsync(
            Phase0ScenarioContext context,
            string barrier,
            string key)
        {
            var store = new InMemoryWorkflowProvider();
            var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
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
            var processor = new DurableCommandProcessor(store, pools);
            var runtime = CreateRuntime(
                store,
                pools,
                definition,
                context.Services.TimeProvider,
                new LeaseServices(pools, new LeaseProbe(), gate),
                processor);
            var running = runtime.StartOrGetAsync<string, LeaseState>(
                key,
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                CancellationToken.None);
            await gate.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var instanceId = (await store.ListAsync(
                new WorkflowProjectionQuery(),
                CancellationToken.None)).Single().InstanceId;
            var envelope = await EnvelopeAsync(store, instanceId);
            var token = LeaseProtectionToken.Parse(
                envelope.OwnedObligations.Single(
                    obligation => obligation.ProtectionToken is not null).ProtectionToken!);
            var definitionHandle = DurableFacadeScenarioAdapter.Register(
                runtime,
                store,
                store,
                new DurableManagement(store, pools, store, processor),
                context.Services.TimeProvider,
                definition,
                [ResourcePoolName.Create("database")]);
            var instance = await definitionHandle.GetInstanceAsync(
                instanceId,
                CancellationToken.None);
            return new ActiveLeaseFixture(
                store,
                pools,
                processor,
                gate,
                running,
                instanceId,
                token,
                instance);
        }
    }

    private sealed class QueuedLeaseFixture
    {
        private QueuedLeaseFixture(
            InMemoryWorkflowProvider store,
            InMemoryResourcePoolStore pools,
            DurableCommandProcessor processor,
            InstanceId instanceId,
            WorkflowInstanceHandle instance)
        {
            Store = store;
            Pools = pools;
            Processor = processor;
            InstanceId = instanceId;
            Instance = instance;
        }

        internal InMemoryWorkflowProvider Store { get; }
        internal InMemoryResourcePoolStore Pools { get; }
        internal DurableCommandProcessor Processor { get; }
        internal InstanceId InstanceId { get; }
        internal WorkflowInstanceHandle Instance { get; }

        internal static async Task<QueuedLeaseFixture> CreateAsync(
            Phase0ScenarioContext context,
            string barrier,
            string key)
        {
            var store = new InMemoryWorkflowProvider();
            var pools = await CreatePoolsAsync(("database", 1, (TimeSpan?)null));
            var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
            _ = await pools.AcquireAsync(
                new ResourcePoolAcquireRequest(
                    blocker,
                    "external",
                    [new ResourcePoolRequirement("database", 1)],
                    context.Services.TimeProvider.GetUtcNow(),
                    ExpiresAt: null),
                CancellationToken.None);
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
            var processor = new DurableCommandProcessor(store, pools);
            var runtime = CreateRuntime(
                store,
                pools,
                definition,
                context.Services.TimeProvider,
                new LeaseServices(pools, new LeaseProbe()),
                processor);
            var started = await runtime.StartOrGetAsync<string, LeaseState>(
                key,
                definition.DefinitionId,
                definition.DefinitionVersion,
                "input",
                CancellationToken.None);
            var definitionHandle = DurableFacadeScenarioAdapter.Register(
                runtime,
                store,
                store,
                new DurableManagement(store, pools, store, processor),
                context.Services.TimeProvider,
                definition,
                [ResourcePoolName.Create("database")]);
            var instance = await definitionHandle.GetInstanceAsync(
                started.InstanceId,
                CancellationToken.None);
            return new QueuedLeaseFixture(store, pools, processor, started.InstanceId, instance);
        }
    }
}
