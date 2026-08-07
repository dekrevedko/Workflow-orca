using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

public sealed class R4DurableEngineFindingsTests
{
    [Fact]
    public async Task R4_StartOrGet_RestartUsesDurableIdempotencyKey()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<R4State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new R4State(value))
            .End(WorkflowOutcomeName.Create("started"))
            .Build();
        var key = StartIdempotencyKey.Create("order-123");
        var firstFacade = CreateFacade(store);
        var firstHandle = firstFacade.Registry.Register(definition).GetHandleOrThrow();
        var first = await firstHandle.StartOrGetAsync(
            "order-123",
            key,
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store);
        var replacementHandle = replacement.Registry.Register(definition).GetHandleOrThrow();
        var second = await replacementHandle.StartOrGetAsync(
            "order-123",
            key,
            TestContext.Current.CancellationToken);
        var firstAccepted = first.Should()
            .BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which;
        var secondAccepted = second.Should()
            .BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which;
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(firstAccepted.Handle.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        firstAccepted.WasExisting.Should().BeFalse();
        secondAccepted.WasExisting.Should().BeTrue();
        secondAccepted.Handle.InstanceId.Should().Be(firstAccepted.Handle.InstanceId);
        events.OfType<WorkflowStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task R4_ResourcePoolAcquire_AppendFailureRollsBackGrantedTicket()
    {
        var store = new RejectAcquireAppendStore();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var definition = LeaseDefinition(DefinitionId.New());
        var facade = CreateFacade(
            store,
            resourcePoolStore: pools,
            stepServices: NoOpStepServices.Instance,
            configuredResourcePools: [ResourcePoolName.Create("db")]);
        var handle = facade.Registry.Register(definition).GetHandleOrThrow();

        var result = await handle.StartOrGetAsync(
            "order-1",
            StartIdempotencyKey.Create("resource-acquire-append-conflict"),
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        store.AcquireAppendRejected.Should().BeTrue();
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task R4_ResourcePoolRelease_RetriesTransientReleaseFailureAfterCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new FlakyReleasePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var definition = LeaseDefinition(DefinitionId.New());
        var facade = CreateFacade(
            store,
            resourcePoolStore: pools,
            stepServices: NoOpStepServices.Instance,
            configuredResourcePools: [ResourcePoolName.Create("db")]);
        var handle = facade.Registry.Register(definition).GetHandleOrThrow();

        var result = await handle.StartOrGetAsync(
            "order-1",
            StartIdempotencyKey.Create("resource-release-retry"),
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        pools.ReleaseAttempts.Should().Be(2);
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public void R4_PublicWorkflowEvents_DoNotExposeBranchIdentity()
    {
        typeof(WorkflowInboundEvent).GetProperty("BranchId").Should().BeNull();
        typeof(WorkflowInboundEvent<string>).GetProperty("BranchId").Should().BeNull();
    }

    [Fact]
    public async Task R4_CheckpointRehydration_RestoresRuntimeCollectionsWithoutTail()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("checkpoint-resume");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(store);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            "order-1",
            StartIdempotencyKey.Create("checkpoint-rehydration"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var checkpoint = await store.LoadCheckpointAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var delivered = await replacement.Events.AcceptAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
                EventId.Create("checkpoint-resume-event"),
                CorrelationId.Create("order-1"),
                causationEventId: null,
                DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
                new WorkflowEventRoute.Direct(instanceId)),
            TestContext.Current.CancellationToken);

        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.RuntimeState.ActiveWaits.Should().ContainSingle();
        delivered.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
    }

    [Fact]
    public async Task R4_ProviderOptimisticConcurrency_RejectsStaleCrossHostAppend()
    {
        var store = new InMemoryWorkflowProvider();
        var streamId = new WorkflowStreamId(InstanceIdValue(1));
        var first = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started()]
            },
            TestContext.Current.CancellationToken);
        var stale = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = [TimerScheduled(TimerIdValue(10))]
            },
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        stale.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void R4_OutboxPump_DoesNotUseUnboundedChannel()
    {
        var source = File.ReadAllText(FindRepoFile("src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs"));

        source.Should().NotContain("Channel.CreateUnbounded");
    }

    private static WorkflowStartedEvent Started()
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout"
        };
    }

    private static DurableWorkflowDefinition<string> WaitingDefinition(
        DefinitionId definitionId,
        EventName eventName)
    {
        return Workflow.Durable<R4State>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new R4State(value))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Value))
            .End(WorkflowOutcomeName.Create("matched"))
            .Build();
    }

    private static DurableWorkflowDefinition<string> LeaseDefinition(DefinitionId definitionId) =>
        Workflow.Durable<R4State>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new R4State(value))
            .AcquireResources(
                ResourceLeaseRequest.Create(
                    ResourceLeaseRequirement.Require(ResourcePoolName.Create("db"))),
                lease => lease.Then<NoOpStep>())
            .End(WorkflowOutcomeName.Create("released"))
            .Build();

    private static FacadeServices CreateFacade<TStore>(
        TStore store,
        IResourcePoolStore? resourcePoolStore = null,
        IServiceProvider? stepServices = null,
        IEnumerable<ResourcePoolName>? configuredResourcePools = null)
        where TStore : IWorkflowEventStore,
            IWorkflowInboxStore,
            IWorkflowStartIdempotencyStore,
            IWorkflowProjectionStore
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(
            store,
            resourcePoolStore,
            notifications);
        var definitions = stepServices is null
            ? new DurableDefinitionRegistry()
            : new DurableDefinitionRegistry(stepServices);
        var runtime = new DurableWorkflowRuntime(
            processor,
            definitions,
            TimeProvider.System,
            projectionStore: store);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                store,
                store,
                processor,
                notifications,
                TimeProvider.System,
                configuredResourcePools),
            new DurableWorkflowEventIngressCore(runtime, store, store));
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}'.");
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 17, minutes, 0, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events);

    private sealed record R4State(string Value);

    private sealed class NoOpStepServices : IServiceProvider
    {
        internal static NoOpStepServices Instance { get; } = new();

        private NoOpStepServices()
        {
        }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(NoOpStep) ? new NoOpStep() : null;
    }

    private sealed class NoOpStep : IStep<R4State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<R4State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class RejectAcquireAppendStore :
        IWorkflowEventStore,
        IWorkflowInboxStore,
        IWorkflowStartIdempotencyStore,
        IWorkflowProjectionStore
    {
        private readonly FakeWorkflowEventStore inner = new();

        internal bool AcquireAppendRejected { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.LoadCheckpointAsync(instanceId, cancellationToken);

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            if (!AcquireAppendRejected &&
                batch.Events.OfType<WorkflowResourcePoolAcquiredEvent>().Any())
            {
                AcquireAppendRejected = true;
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    batch.ExpectedVersion));
            }

            return inner.AppendAsync(batch, cancellationToken);
        }

        public Task<IReadOnlyList<OrcaCore.Abstractions.Durable.WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) =>
            inner.LoadTailAsync(streamId, afterVersion, cancellationToken);

        public Task<Option<InboxRecord>> GetAsync(
            InstanceId instanceId,
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetAsync(instanceId, eventId, cancellationToken);

        public Task<Option<InboxRecord>> GetByEventIdAsync(
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetByEventIdAsync(eventId, cancellationToken);

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            inner.GetStartedAsync(idempotencyKey, cancellationToken);

        public Task ApplyAsync(
            IReadOnlyList<ProjectionWrite> operations,
            CancellationToken cancellationToken) =>
            inner.ApplyAsync(operations, cancellationToken);

        public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.GetAsync(instanceId, cancellationToken);

        public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
            DefinitionId? definitionId,
            EventName eventName,
            CorrelationId correlationId,
            CancellationToken cancellationToken) =>
            inner.FindActiveWaitsAsync(
                definitionId,
                eventName,
                correlationId,
                cancellationToken);

        public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
            CancellationToken cancellationToken) =>
            inner.ListLeaseRecoveryCandidatesAsync(cancellationToken);
    }

    private sealed class FlakyReleasePoolStore : IResourcePoolStore
    {
        private readonly InMemoryResourcePoolStore inner = new();
        private int failuresRemaining = 1;

        internal int ReleaseAttempts { get; private set; }

        public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
        {
            return inner.UpsertPoolAsync(definition, cancellationToken);
        }

        public Task<ResourcePoolAcquireResult> AcquireAsync(
            ResourcePoolAcquireRequest request,
            CancellationToken cancellationToken)
        {
            return inner.AcquireAsync(request, cancellationToken);
        }

        public Task<ResourcePoolReleaseResult> ReleaseAsync(
            ResourcePoolReleaseRequest request,
            CancellationToken cancellationToken)
        {
            ReleaseAttempts++;
            if (failuresRemaining > 0)
            {
                failuresRemaining--;
                throw new InvalidOperationException("Transient release failure.");
            }

            return inner.ReleaseAsync(request, cancellationToken);
        }

        public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
        {
            return inner.GetPoolAsync(poolName, cancellationToken);
        }

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            return inner.ExpireTicketsAsync(now, cancellationToken);
        }

    }
}
