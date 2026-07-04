using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.SqlServer;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
public sealed class SqlServerEventStoreTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();
    private SqlServerWorkflowStore? store;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        store = new SqlServerWorkflowStore(
            container.GetConnectionString(),
            new FixedTimeProvider(MigrationAppliedAt()));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (store is not null)
        {
            await store.DisposeAsync();
        }

        await container.DisposeAsync();
    }

    [Fact]
    public async Task InitializeAsync_AppliesRelationalSqlMigrations()
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            """
            select count(*)
            from dbo.orcacore_schema_migrations
            where migration_id in ('001_initial', '002_claim_leases', '003_resource_pools', '004_history_projections');
            """,
            connection);

        var migrationCount = (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException());

        migrationCount.Should().Be(4);
    }

    [Fact]
    public async Task InitializeAsync_UsesTimeProviderForMigrationJournalTimestamps()
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            """
            select count(*)
            from dbo.orcacore_schema_migrations
            where applied_at = @applied_at;
            """,
            connection);
        command.Parameters.AddWithValue("applied_at", MigrationAppliedAt());

        var count = (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException());

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task InitializeAsync_CreatesResourcePoolTables()
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            """
            select count(*)
            from sys.tables
            where schema_id = schema_id('dbo')
              and name in (
                'orcacore_resource_pools',
                'orcacore_resource_tickets',
                'orcacore_resource_waiters',
                'orcacore_resource_expired_tickets',
                'orcacore_resource_audit');
            """,
            connection);

        var tableCount = (int)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException());

        tableCount.Should().Be(5);
    }

    [Fact]
    public async Task AppendAsync_ProjectionOperations_CommitWithEventsInboxAndOutbox()
    {
        var instanceId = InstanceIdValue(1);
        var inboxEventId = EventIdValue(20);
        var outboxRecordId = OutboxRecordIdValue(30);

        var result = await RequiredStore().AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started(instanceId)],
                InboxOperations = [new InboxWrite(inboxEventId, InboxRecordState.Applied)],
                OutboxRecords = [new OutboxWrite(outboxRecordId, "external-message", [1])],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = Snapshot(instanceId)
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var tail = await RequiredStore().LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var inbox = await RequiredStore().GetAsync(inboxEventId, TestContext.Current.CancellationToken);
        var outbox = await RequiredStore().GetStateAsync(outboxRecordId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        tail.Should().ContainSingle();
        inbox.Value.Should().Be(InboxRecordState.Applied);
        outbox.Value.Should().Be(OutboxRecordState.Pending);
    }

    [Fact]
    public async Task AppendAsync_CommittedData_IsLoadedByNewStoreInstance()
    {
        var instanceId = InstanceIdValue(4);
        var inboxEventId = EventIdValue(21);
        var outboxRecordId = OutboxRecordIdValue(31);

        await RequiredStore().AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started(instanceId)],
                InboxOperations = [new InboxWrite(inboxEventId, InboxRecordState.Applied)],
                OutboxRecords = [new OutboxWrite(outboxRecordId, "external-message", [7])],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = Snapshot(instanceId)
                    }
                ],
                TimerSchedules =
                [
                    new TimerScheduleRequest
                    {
                        TimerId = TimerIdValue(40),
                        InstanceId = instanceId,
                        CommandId = CommandIdValue(40),
                        FireAt = Timestamp(10),
                        WakeupName = "approval-timeout"
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        await using var restarted = new SqlServerWorkflowStore(container.GetConnectionString());
        await restarted.InitializeAsync(TestContext.Current.CancellationToken);

        var tail = await restarted.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var inbox = await restarted.GetAsync(inboxEventId, TestContext.Current.CancellationToken);
        var outbox = await restarted.GetStateAsync(outboxRecordId, TestContext.Current.CancellationToken);
        var projections = await restarted.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        var timers = await restarted.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);

        tail.Should().ContainSingle();
        inbox.Value.Should().Be(InboxRecordState.Applied);
        outbox.Value.Should().Be(OutboxRecordState.Pending);
        projections.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
        timers.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
    }

    private SqlServerWorkflowStore RequiredStore()
    {
        return store ?? throw new InvalidOperationException("SQL Server test store is not initialized.");
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowInstanceSnapshot Snapshot(InstanceId instanceId)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = WorkflowStatus.Running,
            CreatedAt = Timestamp(1),
            UpdatedAt = Timestamp(1)
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static DateTimeOffset MigrationAppliedAt()
    {
        return new DateTimeOffset(2026, 7, 4, 11, 30, 0, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
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

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
