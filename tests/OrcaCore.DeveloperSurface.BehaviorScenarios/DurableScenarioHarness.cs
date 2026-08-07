using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using DurableWorkflowEvent = OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

internal sealed class DurableScenarioProvider :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IMessageDispatcher,
    IResourcePoolStore,
    IDurableResourceGovernanceStore,
    IDisposable
{
    private readonly ServiceProvider services;
    private readonly IWorkflowEventStore eventStore;
    private readonly IWorkflowInboxStore inboxStore;
    private readonly IWorkflowStartIdempotencyStore startStore;
    private readonly IWorkflowOutboxStore outboxStore;
    private readonly IWorkflowProjectionStore projectionStore;
    private readonly ITimerScheduler timerScheduler;
    private readonly IMessageDispatcher dispatcher;
    private readonly IResourcePoolStore resourcePoolStore;
    private readonly IDurableResourceGovernanceStore governanceStore;

    internal Action<ProviderCommitBatch>? AfterSuccessfulAppend { get; set; }

    internal DurableScenarioProvider(TimeProvider? timeProvider = null)
    {
        var registrations = new ServiceCollection();
        if (timeProvider is not null)
        {
            registrations.AddSingleton(timeProvider);
        }

        registrations.AddOrcaCoreInMemoryDurableProvider();
        services = registrations.BuildServiceProvider();
        eventStore = services.GetRequiredService<IWorkflowEventStore>();
        inboxStore = services.GetRequiredService<IWorkflowInboxStore>();
        startStore = services.GetRequiredService<IWorkflowStartIdempotencyStore>();
        outboxStore = services.GetRequiredService<IWorkflowOutboxStore>();
        projectionStore = services.GetRequiredService<IWorkflowProjectionStore>();
        timerScheduler = services.GetRequiredService<ITimerScheduler>();
        dispatcher = services.GetRequiredService<IMessageDispatcher>();
        resourcePoolStore = services.GetRequiredService<IResourcePoolStore>();
        governanceStore = services.GetRequiredService<IDurableResourceGovernanceStore>();
    }

    internal void AddRoleTo(IServiceCollection target)
    {
        target.AddSingleton<IWorkflowEventStore>(this);
        target.AddSingleton<IWorkflowInboxStore>(this);
        target.AddSingleton<IWorkflowStartIdempotencyStore>(this);
        target.AddSingleton<IWorkflowOutboxStore>(this);
        target.AddSingleton<IWorkflowProjectionStore>(this);
        target.AddSingleton<ITimerScheduler>(this);
        target.AddSingleton<IMessageDispatcher>(this);
        target.AddSingleton<IResourcePoolStore>(this);
        target.AddSingleton<IDurableResourceGovernanceStore>(this);
        target.AddSingleton<IDurableProviderRole>(ScenarioProviderRole.Instance);
    }

    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        eventStore.LoadCheckpointAsync(instanceId, cancellationToken);

    public async Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        var result = await eventStore.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            AfterSuccessfulAppend?.Invoke(batch);
        }

        return result;
    }

    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken) =>
        eventStore.LoadTailAsync(streamId, afterVersion, cancellationToken);

    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken) =>
        inboxStore.GetAsync(instanceId, eventId, cancellationToken);

    public Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken) =>
        inboxStore.GetByEventIdAsync(eventId, cancellationToken);

    public Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken) =>
        inboxStore.AcceptAsync(acceptance, cancellationToken);

    public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken) =>
        inboxStore.GetMatchSnapshotAsync(request, cancellationToken);

    public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken) =>
        inboxStore.ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken);

    public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
        DateTimeOffset eligibleAt,
        int maxCount,
        CancellationToken cancellationToken) =>
        inboxStore.ListHandoffRetriesAsync(eligibleAt, maxCount, cancellationToken);

    public Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        inboxStore.MarkPoisonedAsync(eventId, expectedState, code, detail, cancellationToken);

    public Task RecordHandoffFailureAsync(
        EventId eventId,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        inboxStore.RecordHandoffFailureAsync(
            eventId,
            expectedState,
            expectedFailureCount,
            maxFailureCount,
            retryNotBefore,
            code,
            detail,
            cancellationToken);

    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        startStore.GetStartedAsync(idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        int maxCount,
        CancellationToken cancellationToken) =>
        outboxStore.ClaimAsync(maxCount, cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken) =>
        outboxStore.ClaimAsync(request, cancellationToken);

    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        outboxStore.GetStateAsync(outboxRecordId, cancellationToken);

    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken) =>
        outboxStore.MarkAsync(outboxRecordId, state, cancellationToken);

    public Task ReleaseAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        outboxStore.ReleaseAsync(outboxRecordId, cancellationToken);

    public Task ApplyAsync(
        IReadOnlyList<ProjectionWrite> operations,
        CancellationToken cancellationToken) =>
        projectionStore.ApplyAsync(operations, cancellationToken);

    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        projectionStore.GetAsync(instanceId, cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        projectionStore.FindActiveWaitsAsync(
            definitionId,
            eventName,
            correlationId,
            cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken) =>
        projectionStore.ListLeaseRecoveryCandidatesAsync(cancellationToken);

    public Task ScheduleAsync(
        TimerScheduleRequest request,
        CancellationToken cancellationToken) =>
        timerScheduler.ScheduleAsync(request, cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken) =>
        timerScheduler.ClaimDueAsync(dueAtOrBefore, maxCount, cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken) =>
        timerScheduler.ClaimDueAsync(request, cancellationToken);

    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken) =>
        timerScheduler.CompleteAsync(timerId, cancellationToken);

    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken) =>
        timerScheduler.ReleaseAsync(timerId, cancellationToken);

    public Task<DispatchResult> DispatchAsync(
        OutboxWrite record,
        CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(record, cancellationToken);

    public Task UpsertPoolAsync(
        ResourcePoolDefinition definition,
        CancellationToken cancellationToken) =>
        resourcePoolStore.UpsertPoolAsync(definition, cancellationToken);

    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken) =>
        resourcePoolStore.AcquireAsync(request, cancellationToken);

    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken) =>
        resourcePoolStore.ReleaseAsync(request, cancellationToken);

    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
        string poolName,
        CancellationToken cancellationToken) =>
        resourcePoolStore.GetPoolAsync(poolName, cancellationToken);

    public Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(
        CancellationToken cancellationToken) =>
        resourcePoolStore.ListPoolsAsync(cancellationToken);

    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        resourcePoolStore.ExpireTicketsAsync(now, cancellationToken);

    public Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default) =>
        resourcePoolStore.GetReleaseEvidenceAsync(protectionToken, cancellationToken);

    public Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default) =>
        resourcePoolStore.GetConfirmationBindingAsync(confirmationId, cancellationToken);

    public Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default) =>
        resourcePoolStore.ConfirmAndReleaseAsync(request, cancellationToken);

    public Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        resourcePoolStore.PurgeReleaseEvidenceAsync(instanceId, cancellationToken);

    public ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default) =>
        governanceStore.LoadAsync(partitionId, cancellationToken);

    public ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default) =>
        governanceStore.AppendAsync(partitionId, expectedVersion, records, cancellationToken);

    public void Dispose() => services.Dispose();

    private sealed record ScenarioProviderRole : IDurableProviderRole
    {
        internal static ScenarioProviderRole Instance { get; } = new();
        public string Name => "scenario-in-memory";
        public bool IsDevelopmentOnly => true;
    }
}

internal sealed class DurableScenarioRuntime : IDisposable
{
    private readonly ServiceProvider services;
    private readonly IWorkflowDefinitionRegistry registry;
    private readonly ProcessLocalEventRouter events;
    private readonly Dictionary<DefinitionId, object> handles = [];
    private readonly Dictionary<InstanceId, WorkflowInstanceHandle> instances = [];
    private readonly string eventSourceId = Guid.NewGuid().ToString("N");
    private long eventSequence;

    private DurableScenarioRuntime(ServiceProvider services)
    {
        this.services = services;
        registry = services.GetRequiredService<IWorkflowDefinitionRegistry>();
        events = services.GetRequiredService<ProcessLocalEventRouter>();
    }

    internal IServiceProvider Services => services;

    internal IResourcePoolStore ResourcePools => services.GetRequiredService<IResourcePoolStore>();

    internal IDurableResourcePoolManagement ResourceManagement =>
        services.GetRequiredService<IDurableResourcePoolManagement>();

    internal IDurableResourceLeaseRecovery LeaseRecovery =>
        services.GetRequiredService<IDurableResourceLeaseRecovery>();

    internal IDurableResourceLeaseDiagnostics LeaseDiagnostics =>
        services.GetRequiredService<IDurableResourceLeaseDiagnostics>();

    internal static DurableScenarioRuntime Create(
        DurableScenarioProvider provider,
        TimeProvider? timeProvider = null,
        int maxConcurrentExecutionPaths = 4,
        IEnumerable<DurableResourcePoolDefinition>? resourcePools = null,
        Action<IServiceCollection>? configureServices = null,
        string? partition = null,
        Func<IResourcePoolStore, IResourcePoolStore>? decorateResourcePools = null)
    {
        var registrations = new ServiceCollection();
        provider.AddRoleTo(registrations);
        registrations.AddSingleton(timeProvider ?? TimeProvider.System);
        configureServices?.Invoke(registrations);
        registrations.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = maxConcurrentExecutionPaths,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create(
                    partition ?? $"scenario-{Guid.NewGuid():N}"),
                Pools = resourcePools?.ToArray() ?? []
            }
        });
        registrations.AddSingleton<ProcessLocalEventRouter>();
        if (decorateResourcePools is not null)
        {
            var original = registrations.Last(descriptor =>
                descriptor.ServiceType == typeof(IResourcePoolStore));
            registrations.Remove(original);
            registrations.AddSingleton<IResourcePoolStore>(provider =>
                decorateResourcePools(ResolveResourcePoolStore(provider, original)));
        }

        return new DurableScenarioRuntime(registrations.BuildServiceProvider());
    }

    private static IResourcePoolStore ResolveResourcePoolStore(
        IServiceProvider provider,
        ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IResourcePoolStore instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IResourcePoolStore)descriptor.ImplementationFactory(provider);
        }

        return (IResourcePoolStore)ActivatorUtilities.CreateInstance(
            provider,
            descriptor.ImplementationType!);
    }

    internal DurableDefinitionHandle<TInput> Register<TInput>(
        DurableWorkflowDefinition<TInput> definition)
    {
        var handle = registry.Register(definition).GetHandleOrThrow();
        handles[definition.DefinitionId] = handle;
        return handle;
    }

    internal DurableDefinitionHandle<TInput, TOutput> Register<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        var handle = registry.Register(definition).GetHandleOrThrow();
        handles[definition.DefinitionId] = handle;
        return handle;
    }

    internal async ValueTask<DurableScenarioStartResult> StartOrGetAsync<TInput, TState>(
        string idempotencyKey,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TInput input,
        CancellationToken cancellationToken)
    {
        _ = definitionVersion;
        var handle = (DurableDefinitionHandle<TInput>)handles[definitionId];
        var started = await handle.StartOrGetAsync(
            input,
            StartIdempotencyKey.Create(idempotencyKey),
            cancellationToken).ConfigureAwait(false);
        var instance = started.GetHandleOrThrow();
        instances[instance.InstanceId] = instance;
        return new DurableScenarioStartResult(instance.InstanceId, instance);
    }

    internal async ValueTask<ProcessLocalEventRouteResult> RaiseEventAsync(
        InstanceId instanceId,
        string eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref eventSequence);
        return await events.RouteToInstanceAsync(
            instanceId,
            ProcessLocalInboundEvent.Create(
                EventId.Create($"scenario-event-{eventSourceId}-{sequence}"),
                EventName.Create(eventName),
                correlationId,
                DateTimeOffset.UtcNow),
            cancellationToken).ConfigureAwait(false);
    }

    internal ValueTask<WorkflowInstanceSnapshot> GetSnapshotAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        instances[instanceId].GetSnapshotAsync(cancellationToken);

    public void Dispose() => services.Dispose();
}

internal sealed record DurableScenarioStartResult(
    InstanceId InstanceId,
    WorkflowInstanceHandle Handle);
