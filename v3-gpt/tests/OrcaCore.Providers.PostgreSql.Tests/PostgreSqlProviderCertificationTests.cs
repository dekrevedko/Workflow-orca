using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

public sealed class PostgreSqlProviderCertificationTests : EventStoreCertificationTests, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();
    private PostgreSqlWorkflowStore? certificationStore;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        certificationStore = new PostgreSqlWorkflowStore(container.GetConnectionString());
        await certificationStore.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (certificationStore is not null)
        {
            await certificationStore.DisposeAsync();
        }

        await container.DisposeAsync();
    }

    protected override IProviderCertificationFixture CreateFixture()
    {
        return new PostgreSqlProviderCertificationFixture(
            certificationStore ?? throw new InvalidOperationException("PostgreSQL certification store is not initialized."));
    }

    [Fact]
    [Trait("AC", "AC-305")]
    public async Task PostgreSql_DuplicateEventsBeforeAndAfterRestartDedup()
    {
        var eventId = EventIdValue(100);
        await using (var store = await CreateStoreAsync())
        {
            await store.AppendAsync(
                Batch(
                    InstanceIdValue(1),
                    StreamVersion.Empty,
                    inbox: [new InboxWrite(eventId, InboxRecordState.Applied)]),
                TestContext.Current.CancellationToken);
        }

        await using var restarted = await CreateStoreAsync();
        var inbox = await restarted.GetAsync(eventId, TestContext.Current.CancellationToken);
        await restarted.AppendAsync(
            Batch(
                InstanceIdValue(2),
                StreamVersion.Empty,
                inbox: [new InboxWrite(eventId, InboxRecordState.Received)]),
            TestContext.Current.CancellationToken);
        var afterDuplicate = await restarted.GetAsync(eventId, TestContext.Current.CancellationToken);

        inbox.Value.Should().Be(InboxRecordState.Applied);
        afterDuplicate.Value.Should().Be(InboxRecordState.Applied);
    }

    [Fact]
    [Trait("AC", "AC-310")]
    public async Task PostgreSql_OutboxDispatchesOnlyCommittedRecords()
    {
        var store = await CreateStoreAsync();
        var outboxRecordId = OutboxRecordIdValue(10);

        await store.AppendAsync(
            Batch(InstanceIdValue(1), new StreamVersion(1), outbox: [new OutboxWrite(outboxRecordId, "status", [1])]),
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PostgreSql_PurgeNeverRemovesActiveInstancesOrClaimedOutbox()
    {
        var activeInstanceId = InstanceIdValue(1);
        var claimedInstanceId = InstanceIdValue(2);
        var outboxRecordId = OutboxRecordIdValue(20);
        var store = await CreateStoreAsync();
        await store.AppendAsync(
            Batch(activeInstanceId, StreamVersion.Empty, projection: RunningSnapshot(activeInstanceId)),
            TestContext.Current.CancellationToken);
        await store.AppendAsync(
            Batch(
                claimedInstanceId,
                StreamVersion.Empty,
                projection: CompletedSnapshot(claimedInstanceId),
                outbox: [new OutboxWrite(outboxRecordId, "status", [1])]),
            TestContext.Current.CancellationToken);
        await store.ClaimAsync(1, TestContext.Current.CancellationToken);

        var activePurge = await store.PurgeAsync(activeInstanceId, TestContext.Current.CancellationToken);
        var claimedPurge = await store.PurgeAsync(claimedInstanceId, TestContext.Current.CancellationToken);
        var activeTail = await store.LoadTailAsync(
            new WorkflowStreamId(activeInstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var claimedOutbox = await store.GetStateAsync(outboxRecordId, TestContext.Current.CancellationToken);

        activePurge.Purged.Should().BeFalse();
        claimedPurge.Purged.Should().BeFalse();
        activeTail.Should().ContainSingle();
        claimedOutbox.Value.Should().Be(OutboxRecordState.Claimed);
    }

    [Fact]
    public async Task OutboxClaim_ConcurrentWorkers_DoNotClaimSameRecord()
    {
        var store = await CreateStoreAsync();
        var outboxRecordId = OutboxRecordIdValue(30);
        await store.AppendAsync(
            Batch(InstanceIdValue(1), StreamVersion.Empty, outbox: [new OutboxWrite(outboxRecordId, "status", [1])]),
            TestContext.Current.CancellationToken);

        var first = store.ClaimAsync(1, TestContext.Current.CancellationToken);
        var second = store.ClaimAsync(1, TestContext.Current.CancellationToken);
        var claimed = (await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken))
            .SelectMany(records => records)
            .ToArray();

        claimed.Should().ContainSingle()
            .Which.OutboxRecordId.Should().Be(outboxRecordId);
    }

    [Fact]
    public async Task ProjectionQuery_ByWaitCorrelation_ReturnsColdInstances()
    {
        var instanceId = InstanceIdValue(1);
        var store = await CreateStoreAsync();
        await store.AppendAsync(
            Batch(instanceId, StreamVersion.Empty, projection: WaitingSnapshot(instanceId)),
            TestContext.Current.CancellationToken);

        var results = await store.ListAsync(
            new WorkflowProjectionQuery
            {
                ActiveWaitEventName = "Approved",
                ActiveWaitCorrelationId = new CorrelationId("order-1")
            },
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
    }

    private async Task<PostgreSqlWorkflowStore> CreateStoreAsync()
    {
        var store = new PostgreSqlWorkflowStore(container.GetConnectionString());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    private static ProviderCommitBatch Batch(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        IReadOnlyList<InboxWrite>? inbox = null,
        IReadOnlyList<OutboxWrite>? outbox = null,
        WorkflowInstanceSnapshot? projection = null)
    {
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = expectedVersion,
            Events = [StartEvent(instanceId)],
            InboxOperations = inbox ?? [],
            OutboxRecords = outbox ?? [],
            ProjectionOperations = projection is null
                ? []
                : [new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = projection
                }]
        };
    }

    private static WorkflowInstanceSnapshot RunningSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Running);
    }

    private static WorkflowInstanceSnapshot CompletedSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Completed);
    }

    private static WorkflowInstanceSnapshot WaitingSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Waiting) with
        {
            ActiveWaits =
            [
                new ActiveWaitSnapshot
                {
                    WaitId = WaitIdValue(1),
                    EventName = "Approved",
                    CorrelationId = new CorrelationId("order-1"),
                    RegisteredAt = Timestamp(2),
                    Status = "Active",
                    Mode = "Resident"
                }
            ]
        };
    }

    private static WorkflowInstanceSnapshot Snapshot(InstanceId instanceId, WorkflowStatus status)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = status,
            CreatedAt = Timestamp(1),
            UpdatedAt = Timestamp(2)
        };
    }

    private static WorkflowStartedEvent StartEvent(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 15, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
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
        return new DefinitionId(GuidValue(value));
    }

    private static OutboxRecordId OutboxRecordIdValue(int value)
    {
        return new OutboxRecordId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private sealed class PostgreSqlProviderCertificationFixture(PostgreSqlWorkflowStore store)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => store;

        public IWorkflowInboxStore InboxStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
