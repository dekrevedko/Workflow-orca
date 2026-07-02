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
        fixture.FailNextCommitBeforeApply();

        var result = await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                StreamVersion.Empty,
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
        fixture.FailNextCommitBeforeApply();

        await fixture.EventStore.AppendAsync(
            Batch(
                new WorkflowStreamId(InstanceId.New()),
                StreamVersion.Empty,
                outboxRecordId: OutboxRecordId.New()),
            TestContext.Current.CancellationToken);
        var claimed = await fixture.OutboxStore.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
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
    /// Makes the next commit fail before any batch operation is applied.
    /// </summary>
    void FailNextCommitBeforeApply();
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

        public void FailNextCommitBeforeApply()
        {
            provider.FailNextCommitBeforeApply();
        }
    }
}
