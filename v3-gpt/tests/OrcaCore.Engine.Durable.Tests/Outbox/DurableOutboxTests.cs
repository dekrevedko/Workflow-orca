using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Outbox;

public sealed class DurableOutboxTests
{
    [Fact]
    [Trait("AC", "AC-310")]
    public async Task CommitFailure_DoesNotExposeOutboxRecordToDispatcher()
    {
        var store = new FakeWorkflowEventStore();
        store.FailNextCommitBeforeApply();

        await store.AppendAsync(
            Batch(OutboxRecordId.New()),
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    public async Task OutboxPump_ClaimsDispatchesAndMarksSuccess()
    {
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);
        var dispatcher = new FakeMessageDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);

        var count = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(recordId, TestContext.Current.CancellationToken);

        count.Should().Be(1);
        dispatcher.Dispatched.Should().ContainSingle(record => record.OutboxRecordId == recordId);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    [Trait("AC", "DR-AC-029")]
    public async Task ExternalOutboxPump_NeverDispatchesInternalContinuationRecords()
    {
        var continueId = OutboxRecordId.New();
        var externalId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        var batch = Batch(externalId, "status") with
        {
            OutboxRecords =
            [
                new OutboxWrite(continueId, OutboxKinds.Continue, [1]),
                new OutboxWrite(externalId, "status", [2])
            ]
        };
        await store.AppendAsync(batch, TestContext.Current.CancellationToken);
        var dispatcher = new FakeMessageDispatcher();

        var count = await new DurableOutboxPump(store, dispatcher)
            .PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var continuationClaims = await store.ClaimAsync(
            new OutboxClaimRequest(10, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))
            {
                KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue)
            },
            TestContext.Current.CancellationToken);

        count.Should().Be(1);
        dispatcher.Dispatched.Should().ContainSingle().Which.OutboxRecordId.Should().Be(externalId);
        dispatcher.Dispatched.Should().NotContain(record => record.Kind == OutboxKinds.Continue);
        continuationClaims.Should().ContainSingle().Which.OutboxRecordId.Should().Be(continueId);
    }

    [Fact]
    public async Task OutboxPump_DefaultClaimRequest_UsesInjectedTimeProvider()
    {
        var now = new DateTimeOffset(2026, 7, 4, 10, 15, 0, TimeSpan.Zero);
        var store = new CapturingOutboxStore();
        var pump = new DurableOutboxPump(
            store,
            new FakeMessageDispatcher(),
            timeProvider: new FixedTimeProvider(now));

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);

        store.CapturedRequest.Should().NotBeNull();
        store.CapturedRequest!.MaxCount.Should().Be(10);
        store.CapturedRequest.ClaimedAt.Should().Be(now);
        store.CapturedRequest.LeaseDuration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task InMemoryProvider_DefaultClaimRequest_UsesInjectedTimeProvider()
    {
        var initial = new DateTimeOffset(2026, 7, 4, 10, 15, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(initial);
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider(clock);
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);

        var first = await store.ClaimAsync(1, TestContext.Current.CancellationToken);
        clock.SetUtcNow(initial.AddMinutes(4));
        var stillLeased = await store.ClaimAsync(1, TestContext.Current.CancellationToken);
        clock.SetUtcNow(initial.AddMinutes(6));
        var reclaimed = await store.ClaimAsync(1, TestContext.Current.CancellationToken);

        first.Should().ContainSingle(record => record.OutboxRecordId == recordId);
        stillLeased.Should().BeEmpty();
        reclaimed.Should().ContainSingle(record => record.OutboxRecordId == recordId);
    }

    [Fact]
    public async Task OutboxPump_RetryableFailure_LeavesRecordRetryable()
    {
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);
        var dispatcher = new FakeMessageDispatcher(DispatchResult.RetryableFailure);
        var pump = new DurableOutboxPump(store, dispatcher);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(recordId, TestContext.Current.CancellationToken);

        state.Value.Should().Be(OutboxRecordState.Retryable);
    }

    [Fact]
    public async Task OutboxPump_PermanentFailure_MarksPoisoned()
    {
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);
        var dispatcher = new FakeMessageDispatcher(DispatchResult.PermanentFailure);
        var pump = new DurableOutboxPump(store, dispatcher);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(recordId, TestContext.Current.CancellationToken);

        state.Value.Should().Be(OutboxRecordState.Poisoned);
    }

    [Fact]
    public async Task OutboxPump_DispatcherThrows_MarksRecordRetryable()
    {
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);
        var pump = new DurableOutboxPump(store, new ThrowingDispatcher());

        var count = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(recordId, TestContext.Current.CancellationToken);

        count.Should().Be(0);
        state.Value.Should().Be(OutboxRecordState.Retryable);
    }

    [Fact]
    [Trait("AC", "OB-010")]
    public async Task OutboxPump_NotifiesObserverAfterPumpCycle()
    {
        var first = OutboxRecordId.New();
        var second = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(InstanceId.New()),
                ExpectedVersion = StreamVersion.Empty,
                OutboxRecords =
                [
                    new OutboxWrite(first, "status", [1]),
                    new OutboxWrite(second, "external-message", [2])
                ]
            },
            TestContext.Current.CancellationToken);
        var observer = new RecordingObserver();
        var pump = new DurableOutboxPump(
            store,
            new FakeMessageDispatcher(DispatchResult.Success, DispatchResult.RetryableFailure),
            observer);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);

        observer.Observations.Should().ContainSingle()
            .Which.Should().Be(new OutboxPumpObservation(
                ClaimedCount: 2,
                DispatchAttemptCount: 2,
                SuccessCount: 1,
                RetryableFailureCount: 1,
                PermanentFailureCount: 0));
    }

    [Fact]
    [Trait("AC", "OB-011")]
    public async Task OutboxPump_ObserverThrows_DoesNotChangeDispatchOutcome()
    {
        var recordId = OutboxRecordId.New();
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(recordId), TestContext.Current.CancellationToken);
        var pump = new DurableOutboxPump(
            store,
            new FakeMessageDispatcher(),
            new ThrowingOutboxObserver());

        var count = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(recordId, TestContext.Current.CancellationToken);

        count.Should().Be(1);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    [Trait("AC", "DU-033")]
    public async Task OutboxKindMessageDispatcher_DispatchesByKind()
    {
        var childDispatcher = new FakeMessageDispatcher();
        var lifecycleDispatcher = new FakeMessageDispatcher();
        var dispatcher = new OutboxKindMessageDispatcher(
            [
                new OutboxKindDispatcherRoute("child-start", childDispatcher),
                new OutboxKindDispatcherRoute("lifecycle-event", lifecycleDispatcher)
            ]);
        var child = new OutboxWrite(OutboxRecordId.New(), "child-start", [1]);
        var lifecycle = new OutboxWrite(OutboxRecordId.New(), "lifecycle-event", [2]);

        var childResult = await dispatcher.DispatchAsync(child, TestContext.Current.CancellationToken);
        var lifecycleResult = await dispatcher.DispatchAsync(lifecycle, TestContext.Current.CancellationToken);

        childResult.Should().Be(DispatchResult.Success);
        lifecycleResult.Should().Be(DispatchResult.Success);
        childDispatcher.Dispatched.Should().ContainSingle().Which.Should().Be(child);
        lifecycleDispatcher.Dispatched.Should().ContainSingle().Which.Should().Be(lifecycle);
    }

    [Fact]
    public async Task OutboxKindMessageDispatcher_UnknownKind_IsPermanentFailure()
    {
        var dispatcher = new OutboxKindMessageDispatcher([]);
        var record = new OutboxWrite(OutboxRecordId.New(), "missing", [1]);

        var result = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.PermanentFailure);
    }

    [Fact]
    public async Task UnifiedOutbox_CarriesStatusAndExternalMessageRecords()
    {
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(Batch(OutboxRecordId.New(), "status"), TestContext.Current.CancellationToken);
        await store.AppendAsync(
            Batch(OutboxRecordId.New(), "external-message"),
            TestContext.Current.CancellationToken);

        var claimed = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Select(record => record.Kind).Should().BeEquivalentTo("status", "external-message");
    }

    private static ProviderCommitBatch Batch(
        OutboxRecordId outboxRecordId,
        string kind = "status",
        StreamVersion? expectedVersion = null)
    {
        var instanceId = InstanceId.New();
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion ?? StreamVersion.Empty,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = instanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            OutboxRecords = [new OutboxWrite(outboxRecordId, kind, [1])]
        };
    }

    private sealed class ThrowingDispatcher : IMessageDispatcher
    {
        public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("transport unavailable");
        }
    }

    private sealed class RecordingObserver : IOutboxPumpObserver
    {
        internal List<OutboxPumpObservation> Observations { get; } = [];

        public ValueTask OnPumpCompletedAsync(
            OutboxPumpObservation observation,
            CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingOutboxObserver : IOutboxPumpObserver
    {
        public ValueTask OnPumpCompletedAsync(
            OutboxPumpObservation observation,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("telemetry sink unavailable");
        }
    }

    private sealed class CapturingOutboxStore : IWorkflowOutboxStore
    {
        public OutboxClaimRequest? CapturedRequest { get; private set; }

        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
            OutboxClaimRequest request,
            CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return Task.FromResult<IReadOnlyList<OutboxWrite>>([]);
        }

        public Task<Option<OutboxRecordState>> GetStateAsync(
            OutboxRecordId outboxRecordId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task MarkAsync(
            OutboxRecordId outboxRecordId,
            OutboxRecordState state,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset utcNow;

        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            this.utcNow = utcNow;
        }

        public void SetUtcNow(DateTimeOffset value)
        {
            utcNow = value;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
