using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-302")]
    public async Task FailedInlineConsumption_IsLaterAppliedWithoutBrokerResubmission(
        bool throwProviderFailure)
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
        var suffix = throwProviderFailure ? "provider-failure" : "conflict";
        var workflowEvent = RecoveryEvent(instanceId, $"crash-before-commit-{suffix}", eventName, correlation);
        eventStore.FailNextCommitBeforeApply(throwProviderFailure);

        var accepted = await first.Events.AcceptAsync(
            workflowEvent,
            TestContext.Current.CancellationToken);
        var afterFailure = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var replacement = CreateFacade(eventStore, store, store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var inboxPump = new DurableInboxContinuationPump(store, store, replacement.Runtime);
        var continuationPump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            inboxStore: store);

        var matched = await inboxPump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var continued = await continuationPump.PumpOnceAsync(
            new OutboxClaimRequest(10, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var applied = await store.GetByEventIdAsync(
            workflowEvent.EventId,
            TestContext.Current.CancellationToken);

        afterFailure.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        matched.Should().Be(1);
        continued.Should().BePositive();
        applied.Value.State.Should().Be(InboxRecordState.Applied);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task InboxPump_IsolatesRecordsAndRevisitsFailuresWithoutDrainingNewAcceptance()
    {
        var clock = new Clock(DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
        var store = new InMemoryWorkflowProvider();
        var eventStore = new SelectivelyFailingEventStore(store);
        var eventName = EventName.Create("isolated-handoff");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var owner = CreateFacade(eventStore, store, store, driveAfterDelivery: false, clock.TimeProvider);
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        var instances = new Dictionary<string, InstanceId>();
        foreach (var key in new[] { "a", "b", "c", "d", "e" })
        {
            instances[key] = (await handle.StartOrGetAsync(
                new Input($"isolated-{key}"),
                StartIdempotencyKey.Create($"isolated-start-{key}"),
                TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        }

        eventStore.FailCommitsFor(instances["a"], int.MaxValue);
        foreach (var key in new[] { "b", "c", "d", "e" })
        {
            eventStore.FailCommitsFor(instances[key], 1);
        }

        async Task<WorkflowInboundEvent> AcceptAsync(string key)
        {
            var inbound = RecoveryEvent(
                instances[key],
                $"isolated-event-{key}",
                eventName,
                CorrelationId.Create($"isolated-{key}"));
            var result = await owner.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
            result.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
            return inbound;
        }

        var eventA = await AcceptAsync("a");
        var eventB = await AcceptAsync("b");
        var pump = new DurableInboxContinuationPump(
            store,
            store,
            owner.Runtime,
            clock.TimeProvider,
            maxFailuresBeforePoison: 3,
            initialFailureBackoff: TimeSpan.FromSeconds(1));

        (await pump.PumpOnceAsync(2, TestContext.Current.CancellationToken)).Should().Be(1);
        var afterFirstA = await store.GetByEventIdAsync(eventA.EventId, TestContext.Current.CancellationToken);
        var afterFirstB = await store.GetByEventIdAsync(eventB.EventId, TestContext.Current.CancellationToken);
        afterFirstA.Value.State.Should().Be(InboxRecordState.Received);
        afterFirstA.Value.HandoffFailureCount.Should().Be(1);
        afterFirstB.Value.State.Should().Be(InboxRecordState.Applied,
            "one failing record must not abort the rest of its batch");

        pump = new DurableInboxContinuationPump(
            store,
            store,
            owner.Runtime,
            clock.TimeProvider,
            maxFailuresBeforePoison: 3,
            initialFailureBackoff: TimeSpan.FromSeconds(1));

        var eventC = await AcceptAsync("c");
        clock.Advance(TimeSpan.FromSeconds(1));
        (await pump.PumpOnceAsync(2, TestContext.Current.CancellationToken)).Should().Be(1);
        (await store.GetByEventIdAsync(eventC.EventId, TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);

        var eventD = await AcceptAsync("d");
        clock.Advance(TimeSpan.FromSeconds(2));
        (await pump.PumpOnceAsync(2, TestContext.Current.CancellationToken)).Should().Be(1);
        (await store.GetByEventIdAsync(eventD.EventId, TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);

        var eventE = await AcceptAsync("e");
        (await pump.PumpOnceAsync(2, TestContext.Current.CancellationToken)).Should().Be(1);
        var poisonedA = await store.GetByEventIdAsync(eventA.EventId, TestContext.Current.CancellationToken);
        var appliedE = await store.GetByEventIdAsync(eventE.EventId, TestContext.Current.CancellationToken);

        poisonedA.Value.State.Should().Be(InboxRecordState.Poisoned);
        poisonedA.Value.HandoffFailureCount.Should().Be(3);
        poisonedA.Value.PoisonCode.Should().Be("inbox-continuation-failed");
        appliedE.Value.State.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    public async Task CallbackOnlyAcceptance_CommitsHandoffAndCompetingDefinitionOwnersResumeOnce()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("callback-handoff");
        var correlation = CorrelationId.Create("callback-handoff");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var owner = CreateFacade(store, store, store, driveAfterDelivery: true);
        var instance = (await owner.Registry.Register(definition).GetHandleOrThrow().StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("callback-handoff-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var callback = CreateFacade(store, store, store, driveAfterDelivery: false);
        var inbound = RecoveryEvent(instance.InstanceId, "callback-handoff-event", eventName, correlation);

        var accepted = await callback.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
        var beforePump = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var owned = await store.GetByEventIdAsync(inbound.EventId, TestContext.Current.CancellationToken);
        var beforePumpEvents = await store.LoadTailAsync(
            new WorkflowStreamId(instance.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, store, store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(definition).GetHandleOrThrow();
        var firstPump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            inboxStore: store);
        var secondPump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            inboxStore: store);
        var claimed = await Task.WhenAll(
            firstPump.PumpOnceAsync(PumpRequest(), TestContext.Current.CancellationToken),
            secondPump.PumpOnceAsync(PumpRequest(), TestContext.Current.CancellationToken));
        var afterPump = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instance.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        beforePump.Status.Should().Be(WorkflowInstanceStatus.Running,
            "the committed wait match is runnable before a definition owner claims its handoff");
        beforePumpEvents.OfType<WorkflowResumeConsumedEvent>().Should().BeEmpty(
            "definition-less ingress must not run local workflow code");
        owned.Value.State.Should().Be(InboxRecordState.Applied,
            "the callback host commits the event and continuation handoff atomically");
        claimed.Sum().Should().BePositive();
        afterPump.Status.Should().Be(WorkflowInstanceStatus.Completed);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        events.OfType<WorkflowResumeConsumedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task DefinitionFanout_SnapshotsAllCurrentVersionsAndColdOwnerAppliesEachTargetOnce()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var eventName = EventName.Create("definition-fanout-cold");
        var correlation = CorrelationId.Create("definition-fanout-cold");
        var stageEventName = EventName.Create("definition-fanout-stage");
        var versionOne = WaitingDefinition(definitionId, DefinitionVersion.Initial, eventName);
        var versionTwo = StagedFanoutDefinition(
            definitionId,
            new DefinitionVersion(2),
            stageEventName,
            eventName,
            correlation);
        var starter = CreateFacade(store, store, store, driveAfterDelivery: false);
        var versionOneHandle = starter.Registry.Register(versionOne).GetHandleOrThrow();
        var versionTwoHandle = starter.Registry.Register(versionTwo).GetHandleOrThrow();
        var first = (await versionOneHandle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("definition-fanout-v1"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var second = (await versionTwoHandle.StartOrGetAsync(
            new Input("definition-fanout-release-v2"),
            StartIdempotencyKey.Create("definition-fanout-v2"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

        var callbackOnly = CreateFacade(store, store, store, driveAfterDelivery: false);
        var inbound = RecoveryFanoutEvent(
            definitionId,
            "definition-fanout-event",
            eventName,
            correlation);
        var accepted = await callbackOnly.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
        var beforeOwner = await store.ListDefinitionFanoutTargetsAsync(
            inbound.EventId,
            TestContext.Current.CancellationToken);

        var later = (await versionOneHandle.StartOrGetAsync(
            new Input("definition-fanout-later-correlation"),
            StartIdempotencyKey.Create("definition-fanout-later"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var replacement = CreateFacade(store, store, store, driveAfterDelivery: false);
        _ = replacement.Registry.Register(versionOne).GetHandleOrThrow();
        _ = replacement.Registry.Register(versionTwo).GetHandleOrThrow();
        var inboxPump = new DurableInboxContinuationPump(store, store, replacement.Runtime);
        var continuationPump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            inboxStore: store);

        var matched = await inboxPump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var firstContinued = await continuationPump.PumpOnceAsync(
            PumpRequest(),
            TestContext.Current.CancellationToken);
        var staged = await replacement.Events.AcceptAsync(
            RecoveryEvent(
                second.InstanceId,
                "definition-fanout-release-event",
                stageEventName,
                CorrelationId.Create("definition-fanout-release-v2")),
            TestContext.Current.CancellationToken);
        var secondContinued = await continuationPump.PumpOnceAsync(
            PumpRequest(),
            TestContext.Current.CancellationToken);
        var finalContinued = await continuationPump.PumpOnceAsync(
            PumpRequest(),
            TestContext.Current.CancellationToken);
        var duplicate = await callbackOnly.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
        var afterOwner = await store.ListDefinitionFanoutTargetsAsync(
            inbound.EventId,
            TestContext.Current.CancellationToken);
        var firstSnapshot = await first.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var secondSnapshot = await second.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var laterSnapshot = await later.GetSnapshotAsync(TestContext.Current.CancellationToken);

        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        beforeOwner.Select(record => record.InstanceId).Should().BeEquivalentTo(
            [first.InstanceId, second.InstanceId]);
        beforeOwner.Count(record => record.State == InboxRecordState.Applied).Should().Be(1,
            "definition-less ingress may commit the matching wait but must leave execution to an owner");
        beforeOwner.Count(record => record.State == InboxRecordState.Received).Should().Be(1);
        matched.Should().Be(0);
        firstContinued.Should().BePositive();
        staged.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        secondContinued.Should().BePositive();
        finalContinued.Should().BePositive();
        duplicate.Should().BeOfType<WorkflowEventAcceptanceResult.Duplicate>();
        afterOwner.Should().OnlyContain(record => record.State == InboxRecordState.Applied);
        firstSnapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        secondSnapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        laterSnapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        (await store.GetAsync(later.InstanceId, inbound.EventId, TestContext.Current.CancellationToken))
            .HasValue.Should().BeFalse("instances created after the committed snapshot are excluded");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContinuationPump_RequiresExactBindingAndPoisonsTheOwnedInboxEvent(
        bool registerFingerprintDrift)
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var eventName = EventName.Create("exact-binding");
        var correlation = CorrelationId.Create("exact-binding");
        var definition = WaitingDefinition(definitionId, eventName);
        var owner = CreateFacade(store, store, store, driveAfterDelivery: true);
        var instance = (await owner.Registry.Register(definition).GetHandleOrThrow().StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("exact-binding-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var callback = CreateFacade(store, store, store, driveAfterDelivery: false);
        var inbound = RecoveryEvent(instance.InstanceId, "exact-binding-event", eventName, correlation);
        _ = await callback.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);

        var replacement = CreateFacade(store, store, store, driveAfterDelivery: false);
        if (registerFingerprintDrift)
        {
            var drifted = Workflow.Durable<RecoveryState>(definitionId, DefinitionVersion.Initial)
                .Init<Input>(input => new RecoveryState(input.Correlation))
                .Wait(
                    WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
                    state => CorrelationId.Create(state.Value.Correlation))
                .End(WorkflowOutcomeName.Create("drifted"))
                .Build();
            drifted.DefinitionFingerprint.Should().NotBe(definition.DefinitionFingerprint);
            _ = replacement.Registry.Register(drifted).GetHandleOrThrow();
        }
        var pump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            inboxStore: store);

        _ = await pump.PumpOnceAsync(PumpRequest(), TestContext.Current.CancellationToken);
        var poisoned = await store.GetByEventIdAsync(inbound.EventId, TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instance.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting,
            "parked version binding remains a nonterminal operational wait on the application surface");
        poisoned.Value.State.Should().Be(InboxRecordState.Poisoned);
        poisoned.Value.PoisonCode.Should().Be("definition-binding-unavailable");
        events.OfType<WorkflowResumeConsumedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task VersionBindingInboxPoisonFailure_RemainsRetryableInsteadOfBeingDispatched()
    {
        var clock = new Clock(DateTimeOffset.Parse("2026-08-06T13:00:00Z"));
        var store = new InMemoryWorkflowProvider();
        var eventStore = new FailOnceEventStore(store);
        var eventName = EventName.Create("binding-poison-retry");
        var correlation = CorrelationId.Create("binding-poison-retry");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var owner = CreateFacade(eventStore, store, store, driveAfterDelivery: true, clock.TimeProvider);
        var instance = (await owner.Registry.Register(definition).GetHandleOrThrow().StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("binding-poison-retry-start"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var callback = CreateFacade(eventStore, store, store, driveAfterDelivery: false, clock.TimeProvider);
        var inbound = RecoveryEvent(
            instance.InstanceId,
            "binding-poison-retry-event",
            eventName,
            correlation);
        _ = await callback.Events.AcceptAsync(inbound, TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(
            new OutboxClaimRequest(10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        claimed.Should().NotBeEmpty();
        foreach (var record in claimed)
        {
            await store.ReleaseAsync(record.OutboxRecordId, TestContext.Current.CancellationToken);
        }

        var replacement = CreateFacade(eventStore, store, eventStore, driveAfterDelivery: false, clock.TimeProvider);
        var pump = new DurableContinuationPump(
            store,
            replacement.Runtime,
            replacement.Processor,
            clock.TimeProvider,
            inboxStore: eventStore);
        eventStore.FailNextPoisonWrite();

        _ = await pump.PumpOnceAsync(
            new OutboxClaimRequest(10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        foreach (var record in claimed)
        {
            (await store.GetStateAsync(record.OutboxRecordId, TestContext.Current.CancellationToken))
                .Value.Should().Be(OutboxRecordState.Retryable);
        }
        (await store.GetByEventIdAsync(inbound.EventId, TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxRecordState.Applied);

        clock.Advance(TimeSpan.FromSeconds(1));
        _ = await pump.PumpOnceAsync(
            new OutboxClaimRequest(10, clock.Now, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        foreach (var record in claimed)
        {
            (await store.GetStateAsync(record.OutboxRecordId, TestContext.Current.CancellationToken))
                .Value.Should().Be(OutboxRecordState.Poisoned);
        }
        (await store.GetByEventIdAsync(inbound.EventId, TestContext.Current.CancellationToken))
            .Value.PoisonCode.Should().Be("definition-binding-unavailable");
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
        => WaitingDefinition(definitionId, DefinitionVersion.Initial, eventName);

    private static DurableWorkflowDefinition<Input> WaitingDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        EventName eventName)
    {
        return Workflow.Durable<RecoveryState>(definitionId, definitionVersion)
            .Init<Input>(input => new RecoveryState(input.Correlation))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Correlation))
            .End(WorkflowOutcomeName.Create("recovered"))
            .Build();
    }

    private static DurableWorkflowDefinition<Input> StagedFanoutDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        EventName stageEventName,
        EventName fanoutEventName,
        CorrelationId fanoutCorrelation)
    {
        return Workflow.Durable<RecoveryState>(definitionId, definitionVersion)
            .Init<Input>(input => new RecoveryState(input.Correlation))
            .Wait(
                WorkflowEventContract.Create(stageEventName, EventContractVersion.Initial),
                state => CorrelationId.Create(state.Value.Correlation))
            .Wait(
                WorkflowEventContract.Create(fanoutEventName, EventContractVersion.Initial),
                _ => fanoutCorrelation)
            .End(WorkflowOutcomeName.Create("recovered"))
            .Build();
    }

    private static WorkflowInboundEvent RecoveryFanoutEvent(
        DefinitionId definitionId,
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
            new WorkflowEventRoute.DefinitionFanout(definitionId));
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

    private static OutboxClaimRequest PumpRequest() =>
        new(100, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

    private static FacadeServices CreateFacade(
        IWorkflowEventStore eventStore,
        IWorkflowProjectionStore projectionStore,
        IWorkflowInboxStore inboxStore,
        bool driveAfterDelivery,
        TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(eventStore, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            timeProvider,
            projectionStore: projectionStore);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                projectionStore,
                eventStore,
                processor,
                notifications,
                timeProvider),
            new DurableWorkflowEventIngressCore(
                runtime,
                projectionStore,
                inboxStore,
                driveAfterDelivery),
            runtime,
            processor);
    }

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events,
        DurableWorkflowRuntime Runtime,
        DurableCommandProcessor Processor);

    private sealed class FailOnceEventStore(InMemoryWorkflowProvider inner) :
        IWorkflowEventStore,
        IWorkflowInboxStore,
        IWorkflowStartIdempotencyStore
    {
        private int failNextCommit;
        private int failNextPoison;

        internal void FailNextCommitBeforeApply(bool throwProviderFailure)
        {
            Interlocked.Exchange(ref failNextCommit, throwProviderFailure ? 2 : 1);
        }

        internal void FailNextPoisonWrite()
        {
            Interlocked.Exchange(ref failNextPoison, 1);
        }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.LoadCheckpointAsync(instanceId, cancellationToken);

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            return Interlocked.Exchange(ref failNextCommit, 0) switch
            {
                1 => Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    batch.ExpectedVersion)),
                2 => Task.FromException<Result<AppendEventsResult>>(
                    new InvalidOperationException("Injected provider failure after inbox ownership.")),
                _ => inner.AppendAsync(batch, cancellationToken)
            };
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

        public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
            long afterAcceptanceSequence,
            int maxCount,
            CancellationToken cancellationToken) =>
            inner.ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken);

        public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
            DateTimeOffset eligibleAt,
            int maxCount,
            CancellationToken cancellationToken) =>
            inner.ListHandoffRetriesAsync(eligibleAt, maxCount, cancellationToken);

        public Task MarkPoisonedAsync(
            EventId eventId,
            InboxRecordState expectedState,
            string code,
            string? detail,
            CancellationToken cancellationToken)
        {
            return Interlocked.Exchange(ref failNextPoison, 0) == 1
                ? Task.FromException(new InvalidOperationException("Injected inbox poison-write failure."))
                : inner.MarkPoisonedAsync(eventId, expectedState, code, detail, cancellationToken);
        }

        public Task RecordHandoffFailureAsync(
            EventId eventId,
            InboxRecordState expectedState,
            int expectedFailureCount,
            int maxFailureCount,
            DateTimeOffset retryNotBefore,
            string code,
            string? detail,
            CancellationToken cancellationToken) =>
            inner.RecordHandoffFailureAsync(
                eventId,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken);
    }

    private sealed class SelectivelyFailingEventStore(InMemoryWorkflowProvider inner) :
        IWorkflowEventStore,
        IWorkflowInboxStore
    {
        private readonly object gate = new();
        private readonly Dictionary<InstanceId, int> remainingFailures = [];

        internal void FailCommitsFor(InstanceId instanceId, int count)
        {
            lock (gate)
            {
                remainingFailures[instanceId] = count;
            }
        }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) =>
            inner.LoadCheckpointAsync(instanceId, cancellationToken);

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                if (remainingFailures.TryGetValue(batch.StreamId.InstanceId, out var remaining) && remaining > 0)
                {
                    if (remaining != int.MaxValue)
                    {
                        remainingFailures[batch.StreamId.InstanceId] = remaining - 1;
                    }

                    return Task.FromException<Result<AppendEventsResult>>(
                        new InvalidOperationException("Injected persistent provider failure."));
                }
            }

            return inner.AppendAsync(batch, cancellationToken);
        }

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) =>
            inner.LoadTailAsync(streamId, afterVersion, cancellationToken);

        public Task<InboxAcceptanceCommitResult> AcceptAsync(
            InboxAcceptance acceptance,
            CancellationToken cancellationToken) =>
            inner.AcceptAsync(acceptance, cancellationToken);

        public Task<Option<InboxRecord>> GetAsync(
            InstanceId instanceId,
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetAsync(instanceId, eventId, cancellationToken);

        public Task<Option<InboxRecord>> GetByEventIdAsync(
            EventId eventId,
            CancellationToken cancellationToken) =>
            inner.GetByEventIdAsync(eventId, cancellationToken);

        public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
            InboxMatchRequest request,
            CancellationToken cancellationToken) =>
            inner.GetMatchSnapshotAsync(request, cancellationToken);

        public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
            long afterAcceptanceSequence,
            int maxCount,
            CancellationToken cancellationToken) =>
            inner.ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken);

        public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
            DateTimeOffset eligibleAt,
            int maxCount,
            CancellationToken cancellationToken) =>
            inner.ListHandoffRetriesAsync(eligibleAt, maxCount, cancellationToken);

        public Task MarkPoisonedAsync(
            EventId eventId,
            InboxRecordState expectedState,
            string code,
            string? detail,
            CancellationToken cancellationToken) =>
            inner.MarkPoisonedAsync(eventId, expectedState, code, detail, cancellationToken);

        public Task RecordHandoffFailureAsync(
            EventId eventId,
            InboxRecordState expectedState,
            int expectedFailureCount,
            int maxFailureCount,
            DateTimeOffset retryNotBefore,
            string code,
            string? detail,
            CancellationToken cancellationToken) =>
            inner.RecordHandoffFailureAsync(
                eventId,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken);
    }

    private sealed record Input(string Correlation);

    private sealed record RecoveryState(string Correlation);
}
