using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class EventStoreCertificationTests
{
    protected abstract IProviderCertificationFixture CreateFixture();

    [Fact]
    [Trait("AC", "AC-309")]
    public async Task ConcurrentAppend_SameExpectedVersion_OneWinnerOneConflict()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.New());
        var first = fixture.EventStore.AppendAsync(
            Batch(streamId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);
        var second = fixture.EventStore.AppendAsync(
            Batch(streamId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-114")]
    public async Task CommitFailure_BeforeApply_LeavesWaitAndInboxEventAvailable()
    {
        var fixture = CreateFixture();
        var inboxEventId = EventId.New();

        var result = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                new StreamVersion(1),
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);
        var inboxRecord = await fixture.InboxStore.GetAsync(
            inboxEventId,
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        inboxRecord.HasValue.Should().BeFalse();
    }

    [Fact]
    [Trait("AC", "AC-305")]
    public async Task InboxDuplicate_AfterRecordedApplied_IsIgnored()
    {
        var fixture = CreateFixture();
        var inboxEventId = EventId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                StreamVersion.Empty,
                inboxEventId: inboxEventId),
            TestContext.Current.CancellationToken);

        var recorded = await fixture.InboxStore.GetAsync(inboxEventId, TestContext.Current.CancellationToken);

        recorded.HasValue.Should().BeTrue();
        recorded.Value.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    [Trait("AC", "AC-310")]
    public async Task OutboxRecords_AreNotVisibleWhenCommitFails()
    {
        var fixture = CreateFixture();

        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                new StreamVersion(1),
                outboxRecordId: OutboxRecordId.New()),
            TestContext.Current.CancellationToken);
        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-003")]
    [Trait("AC", "PR-010")]
    public async Task NEG_PR_003_AppendAsync_EmptyBatchIsNoOpAndDoesNotAdvanceStream()
    {
        var fixture = CreateFixture();
        var streamId = new WorkflowStreamId(InstanceId.New());

        var result = await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty
            },
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            streamId,
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewVersion.Should().Be(StreamVersion.Empty);
        tail.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-006")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_006_ClaimAsync_WithNoPendingOutboxReturnsEmptyBatch()
    {
        var fixture = CreateFixture();

        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-007")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_007_MarkAsync_UnknownOutboxRecordDoesNotCreateState()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();

        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        var state = await fixture.OutboxStore.GetStateAsync(
            outboxRecordId,
            TestContext.Current.CancellationToken);

        state.HasValue.Should().BeFalse();
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-008")]
    [Trait("AC", "DU-032")]
    public async Task NEG_PR_008_MarkAsync_DispatchedOutboxRecordTwiceIsIdempotent()
    {
        var fixture = CreateFixture();
        var outboxRecordId = OutboxRecordId.New();
        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                StreamVersion.Empty,
                outboxRecordId: outboxRecordId),
            TestContext.Current.CancellationToken);

        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);
        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        await fixture.OutboxStore.MarkAsync(
            outboxRecordId,
            OutboxRecordState.Dispatched,
            TestContext.Current.CancellationToken);
        var state = await fixture.OutboxStore.GetStateAsync(
            outboxRecordId,
            TestContext.Current.CancellationToken);
        var afterDispatch = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().ContainSingle(record => record.OutboxRecordId == outboxRecordId);
        state.Value.Should().Be(OutboxRecordState.Dispatched);
        afterDispatch.Should().BeEmpty();
    }

    private static ProviderCommitBatch Batch(
        WorkflowStreamId streamId,
        StreamVersion expectedVersion,
        EventId? inboxEventId = null,
        OutboxRecordId? outboxRecordId = null)
    {
        return new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = expectedVersion,
            Events =
            [
                new WorkflowStartedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = streamId.InstanceId,
                    CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            InboxOperations = inboxEventId is { } eventId
                ? [new InboxWrite(eventId, InboxRecordState.Applied)]
                : [],
            OutboxRecords = outboxRecordId is { } recordId
                ? [new OutboxWrite(recordId, "status", [1])]
                : []
        };
    }
}

/// <summary>
/// Supplies the provider ports exercised by provider certification tests.
/// </summary>
public interface IProviderCertificationFixture
{
    /// <summary>
    /// Gets the event store under certification.
    /// </summary>
    IWorkflowEventStore EventStore { get; }

    /// <summary>
    /// Gets the inbox store observed by certification tests.
    /// </summary>
    IWorkflowInboxStore InboxStore { get; }

    /// <summary>
    /// Gets the outbox store observed by certification tests.
    /// </summary>
    IWorkflowOutboxStore OutboxStore { get; }

    /// <summary>
    /// Gets the projection store observed by certification tests.
    /// </summary>
    IWorkflowProjectionStore ProjectionStore { get; }
}

public sealed class FakeEventStoreCertificationTests : EventStoreCertificationTests
{
    protected override IProviderCertificationFixture CreateFixture()
    {
        return new FakeProviderCertificationFixture(new FakeWorkflowEventStore());
    }

    private sealed class FakeProviderCertificationFixture(FakeWorkflowEventStore provider)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider;

        public IWorkflowInboxStore InboxStore => provider;

        public IWorkflowOutboxStore OutboxStore => provider;

        public IWorkflowProjectionStore ProjectionStore => provider;
    }
}
