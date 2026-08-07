using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Recovery;

public sealed class DurableRecoveryTests
{
    [Fact]
    [Trait("AC", "AC-301")]
    public async Task WaitingInstance_RehydrateAfterRestart_RemainsResumable()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("recovery-resume");
        var correlation = CorrelationId.Create("waiting-restart");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(store, store, store, driveAfterDelivery: true);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("waiting-restart-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;

        var replacement = CreateFacade(store, store, store, driveAfterDelivery: true);
        var replacementHandle = replacement.Registry.Register(definition).GetHandleOrThrow();
        var result = await replacement.Events.AcceptAsync(
            RecoveryEvent(instanceId, "waiting-restart-event", eventName, correlation),
            TestContext.Current.CancellationToken);
        var snapshot = await (await replacementHandle.GetInstanceAsync(
                instanceId,
                TestContext.Current.CancellationToken))
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

        result.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    [Trait("AC", "AC-302")]
    public async Task CrashBeforeCommit_RehydratesLastCommittedStateOnly()
    {
        var store = new InMemoryWorkflowProvider();
        var eventStore = new FailOnceEventStore(store);
        var eventName = EventName.Create("crash-recovery");
        var correlation = CorrelationId.Create("crash-before-commit");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var first = CreateFacade(eventStore, store, store, driveAfterDelivery: false);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("crash-before-commit-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var workflowEvent = RecoveryEvent(instanceId, "crash-before-commit-event", eventName, correlation);
        eventStore.FailNextCommitBeforeApply();

        var accepted = await first.Events.AcceptAsync(
            workflowEvent,
            TestContext.Current.CancellationToken);
        var afterFailure = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var replacement = CreateFacade(eventStore, store, store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var recovered = await replacement.Events.AcceptAsync(
            workflowEvent,
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        afterFailure.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        recovered.Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-316")]
    public async Task HostKilledBeforeShutdownHook_LosesNoCommittedTransition()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("committed-transition");
        var correlation = CorrelationId.Create("host-killed");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var idempotencyKey = StartIdempotencyKey.Create("host-killed-start");
        var first = CreateFacade(store, store, store, driveAfterDelivery: false);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            idempotencyKey,
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var accepted = await first.Events.AcceptAsync(
            RecoveryEvent(instanceId, "host-killed-event", eventName, correlation),
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, store, store, driveAfterDelivery: true);
        var replacementHandle = replacement.Registry.Register(definition).GetHandleOrThrow();
        var restarted = await replacementHandle.StartOrGetAsync(
            new Input(correlation.Value),
            idempotencyKey,
            TestContext.Current.CancellationToken);
        var snapshot = await restarted.GetHandleOrThrow()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        restarted.Should()
            .BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task CheckpointPlusTail_RehydratesWithoutGenesisReplay()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("checkpoint-tail");
        var correlation = CorrelationId.Create("checkpoint-tail");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var idempotencyKey = StartIdempotencyKey.Create("checkpoint-tail-start");
        var first = CreateFacade(store, store, store, driveAfterDelivery: false);
        var firstHandle = first.Registry.Register(definition).GetHandleOrThrow();
        var instanceId = (await firstHandle.StartOrGetAsync(
            new Input(correlation.Value),
            idempotencyKey,
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var checkpoint = await store.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken);

        await first.Events.AcceptAsync(
            RecoveryEvent(instanceId, "checkpoint-tail-event", eventName, correlation),
            TestContext.Current.CancellationToken);
        var replacement = CreateFacade(store, store, store, driveAfterDelivery: true);
        var replacementHandle = replacement.Registry.Register(definition).GetHandleOrThrow();
        var restarted = await replacementHandle.StartOrGetAsync(
            new Input(correlation.Value),
            idempotencyKey,
            TestContext.Current.CancellationToken);
        var snapshot = await restarted.GetHandleOrThrow()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);
        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            checkpoint.Value.StreamVersion,
            TestContext.Current.CancellationToken);

        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.StreamVersion.Value.Should().BeGreaterThan(0);
        tail.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    private static DurableWorkflowDefinition<Input> WaitingDefinition(
        DefinitionId definitionId,
        EventName eventName)
    {
        return Workflow.Durable<RecoveryState>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new RecoveryState(input.Correlation))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Correlation))
            .End(WorkflowOutcomeName.Create("recovered"))
            .Build();
    }

    private static WorkflowInboundEvent RecoveryEvent(
        InstanceId instanceId,
        string eventId,
        EventName eventName,
        CorrelationId correlation)
    {
        return WorkflowInboundEvent.Create(
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            EventId.Create(eventId),
            correlation,
            causationEventId: null,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
            new WorkflowEventRoute.Direct(instanceId));
    }

    private static FacadeServices CreateFacade(
        IWorkflowEventStore eventStore,
        IWorkflowProjectionStore projectionStore,
        IWorkflowInboxStore inboxStore,
        bool driveAfterDelivery)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(eventStore, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: projectionStore);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                projectionStore,
                eventStore,
                processor,
                notifications,
                TimeProvider.System),
            new DurableWorkflowEventIngressCore(
                runtime,
                projectionStore,
                inboxStore,
                driveAfterDelivery));
    }

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events);

    private sealed class FailOnceEventStore(InMemoryWorkflowProvider inner) :
        IWorkflowEventStore,
        IWorkflowInboxStore,
        IWorkflowStartIdempotencyStore
    {
        private int failNextCommit;

        internal void FailNextCommitBeforeApply()
        {
            Interlocked.Exchange(ref failNextCommit, 1);
        }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.LoadCheckpointAsync(instanceId, cancellationToken);

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            return Interlocked.Exchange(ref failNextCommit, 0) == 1
                ? Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    batch.ExpectedVersion))
                : inner.AppendAsync(batch, cancellationToken);
        }

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) =>
            inner.LoadTailAsync(streamId, afterVersion, cancellationToken);

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            inner.GetStartedAsync(idempotencyKey, cancellationToken);

        public Task<Option<InboxRecord>> GetAsync(
            InstanceId instanceId,
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetAsync(instanceId, eventId, cancellationToken);

        public Task<Option<InboxRecord>> GetByEventIdAsync(
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetByEventIdAsync(eventId, cancellationToken);

        public Task<InboxAcceptanceCommitResult> AcceptAsync(
            InboxAcceptance acceptance,
            CancellationToken cancellationToken) =>
            inner.AcceptAsync(acceptance, cancellationToken);

        public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
            InboxMatchRequest request,
            CancellationToken cancellationToken) =>
            inner.GetMatchSnapshotAsync(request, cancellationToken);

        public Task MarkPoisonedAsync(
            EventId eventId,
            string code,
            string? detail,
            CancellationToken cancellationToken) =>
            inner.MarkPoisonedAsync(eventId, code, detail, cancellationToken);
    }

    private sealed record Input(string Correlation);

    private sealed record RecoveryState(string Correlation);
}
