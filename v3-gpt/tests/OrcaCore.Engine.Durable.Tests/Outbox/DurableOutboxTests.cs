using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
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
}
