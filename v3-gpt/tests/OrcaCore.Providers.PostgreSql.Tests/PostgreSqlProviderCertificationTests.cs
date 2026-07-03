using AwesomeAssertions;
using Npgsql;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

public sealed class PostgreSqlProviderCertificationTests : ContinueAsNewCertificationTests, IAsyncLifetime
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
    public async Task InitializeAsync_AppliesRelationalSqlMigrations()
    {
        var initialMigrationId = await ScalarAsync<string>(
            "select migration_id from orcacore_schema_migrations where migration_id = @migration_id;",
            "001_initial");
        var leaseMigrationId = await ScalarAsync<string>(
            "select migration_id from orcacore_schema_migrations where migration_id = @migration_id;",
            "002_claim_leases");
        var startIdempotencyMigrationId = await ScalarAsync<string>(
            "select migration_id from orcacore_schema_migrations where migration_id = @migration_id;",
            "003_start_idempotency");

        initialMigrationId.Should().Be("001_initial");
        leaseMigrationId.Should().Be("002_claim_leases");
        startIdempotencyMigrationId.Should().Be("003_start_idempotency");
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
    [Trait("AC", "AC-311")]
    public async Task PostgreSql_StartIdempotencyPersistsAcrossRestart()
    {
        var instanceId = InstanceIdValue(901);
        const string IdempotencyKey = "order-901";
        await using (var store = await CreateStoreAsync())
        {
            await store.AppendAsync(
                new ProviderCommitBatch
                {
                    StreamId = new WorkflowStreamId(instanceId),
                    ExpectedVersion = StreamVersion.Empty,
                    Events = [StartEvent(instanceId)],
                    StartIdempotencyOperations =
                    [
                        new StartIdempotencyWrite(
                            IdempotencyKey,
                            instanceId,
                            DefinitionIdValue(1),
                            DefinitionVersion.Initial)
                    ]
                },
                TestContext.Current.CancellationToken);
        }

        await using var restarted = await CreateStoreAsync();
        var idempotencyStore = (IWorkflowStartIdempotencyStore)restarted;
        var existing = await idempotencyStore.GetStartedAsync(
            IdempotencyKey,
            TestContext.Current.CancellationToken);

        existing.HasValue.Should().BeTrue();
        existing.Value.InstanceId.Should().Be(instanceId);
        existing.Value.DefinitionId.Should().Be(DefinitionIdValue(1));
        existing.Value.DefinitionVersion.Should().Be(DefinitionVersion.Initial);
    }

    [Fact]
    [Trait("AC", "AC-311")]
    public async Task PostgreSql_DuplicateStartIdempotencyKey_RollsBackAppend()
    {
        const string IdempotencyKey = "order-duplicate";
        var firstInstanceId = InstanceIdValue(902);
        var duplicateInstanceId = InstanceIdValue(903);
        await using var store = await CreateStoreAsync();
        var first = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(firstInstanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [StartEvent(firstInstanceId)],
                StartIdempotencyOperations =
                [
                    new StartIdempotencyWrite(
                        IdempotencyKey,
                        firstInstanceId,
                        DefinitionIdValue(1),
                        DefinitionVersion.Initial)
                ]
            },
            TestContext.Current.CancellationToken);

        var duplicate = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(duplicateInstanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [StartEvent(duplicateInstanceId)],
                StartIdempotencyOperations =
                [
                    new StartIdempotencyWrite(
                        IdempotencyKey,
                        duplicateInstanceId,
                        DefinitionIdValue(2),
                        new DefinitionVersion(2))
                ]
            },
            TestContext.Current.CancellationToken);
        var duplicateTail = await store.LoadTailAsync(
            new WorkflowStreamId(duplicateInstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var existing = await ((IWorkflowStartIdempotencyStore)store).GetStartedAsync(
            IdempotencyKey,
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        duplicate.IsFailure.Should().BeTrue();
        duplicateTail.Should().BeEmpty();
        existing.Value.InstanceId.Should().Be(firstInstanceId);
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
    [Trait("AC", "AC-309")]
    public async Task PostgreSql_ExpectedVersionConflict_ReportsActualVersion()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(50);
        await store.AppendAsync(
            Batch(instanceId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);

        var conflict = await store.AppendAsync(
            Batch(instanceId, StreamVersion.Empty),
            TestContext.Current.CancellationToken);

        conflict.IsFailure.Should().BeTrue();
        conflict.Error.Message.Should().Contain("actual stream version is '1'");
    }

    [Fact]
    [Trait("AC", "EV-050")]
    public async Task PostgreSql_AppendBatch_CommitsTimerScheduleAtomically()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(51);
        var timer = new TimerScheduleRequest
        {
            TimerId = TimerIdValue(51),
            InstanceId = instanceId,
            CommandId = CommandIdValue(51),
            FireAt = Timestamp(10),
            WakeupName = "approval-timeout"
        };

        await store.AppendAsync(
            Batch(instanceId, StreamVersion.Empty) with { TimerSchedules = [timer] },
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);

        claimed.Should().ContainSingle()
            .Which.TimerId.Should().Be(timer.TimerId);
    }

    [Fact]
    [Trait("AC", "EV-050")]
    public async Task PostgreSql_AppendBatch_WhenCommitFails_DoesNotLeaveTimerSchedule()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(52);
        var timer = new TimerScheduleRequest
        {
            TimerId = TimerIdValue(52),
            InstanceId = instanceId,
            CommandId = CommandIdValue(52),
            FireAt = Timestamp(10),
            WakeupName = "approval-timeout"
        };

        await store.AppendAsync(
            Batch(instanceId, new StreamVersion(1)) with { TimerSchedules = [timer] },
            TestContext.Current.CancellationToken);
        var claimed = await store.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "DU-071")]
    public async Task PostgreSql_AppendHistoryProjection_PersistsHistoryRow()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(53);
        await store.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.AppendHistory)
                {
                    History = new ProjectionHistoryWrite(GuidValue(53), Timestamp(11), "operator-note", """{"message":"created"}""")
                }
            ],
            TestContext.Current.CancellationToken);

        var count = await ScalarAsync<long>(
            "select count(*) from orcacore_history_projections where instance_id = @instance_id;",
            instanceId);

        count.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "DU-070")]
    public async Task PostgreSql_CountAsync_UsesScalarQueryWithoutDeserializingProjectionPayloads()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(54);
        await store.ApplyAsync(
            [new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = RunningSnapshot(instanceId)
            }],
            TestContext.Current.CancellationToken);
        await ExecuteAsync(
            """update orcacore_instance_projections set saga_audits = '{"not":"an-array"}'::jsonb where instance_id = @instance_id;""",
            instanceId);

        var count = await store.CountAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);

        count.Should().Be(1);
    }

    [Fact]
    public async Task PostgreSql_ClaimDueAsync_LeasesTimerRowsUntilCompleted()
    {
        var store = await CreateStoreAsync();
        var request = new TimerScheduleRequest
        {
            TimerId = TimerIdValue(55),
            InstanceId = InstanceIdValue(55),
            CommandId = CommandIdValue(55),
            FireAt = Timestamp(10),
            WakeupName = "approval-timeout"
        };
        await store.ScheduleAsync(request, TestContext.Current.CancellationToken);

        var claimed = await store.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);
        var leasedCount = await ScalarAsync<long>(
            "select count(*) from orcacore_timers where timer_id = @instance_id;",
            new InstanceId(request.TimerId.Value));
        var secondClaim = await store.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);

        await store.CompleteAsync(request.TimerId, TestContext.Current.CancellationToken);
        var completedCount = await ScalarAsync<long>(
            "select count(*) from orcacore_timers where timer_id = @instance_id;",
            new InstanceId(request.TimerId.Value));

        claimed.Should().ContainSingle()
            .Which.TimerId.Should().Be(request.TimerId);
        leasedCount.Should().Be(1);
        secondClaim.Should().BeEmpty();
        completedCount.Should().Be(0);
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

    [Fact]
    [Trait("AC", "AC-407")]
    public async Task PostgreSql_SagaEventsAndAuditProjectionRoundTrip()
    {
        var instanceId = InstanceIdValue(1);
        var store = await CreateStoreAsync();
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    StartEvent(instanceId),
                    new SagaForwardActionCompletedEvent
                    {
                        EventId = EventIdValue(101),
                        InstanceId = instanceId,
                        CommandId = CommandIdValue(2),
                        CausationId = CausationIdValue(2),
                        OccurredAt = Timestamp(2),
                        ScopeId = "checkout",
                        ActionKey = "reserve",
                        CompensationKey = "release"
                    }
                ],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = SagaSnapshot(instanceId)
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var projections = await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);

        tail.OfType<SagaForwardActionCompletedEvent>().Should().ContainSingle()
            .Which.CompensationKey.Should().Be("release");
        projections.Should().ContainSingle()
            .Which.SagaAudits.Should().ContainSingle()
            .Which.CompensationActions.Should().ContainSingle()
            .Which.Status.Should().Be(SagaCompensationActionStatus.Completed);
    }

    private async Task<PostgreSqlWorkflowStore> CreateStoreAsync()
    {
        var store = new PostgreSqlWorkflowStore(container.GetConnectionString());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    private async Task<T> ScalarAsync<T>(string sql, InstanceId instanceId)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException());
    }

    private async Task<T> ScalarAsync<T>(string sql, string migrationId)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("migration_id", migrationId);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? throw new InvalidOperationException());
    }

    private async Task ExecuteAsync(string sql, InstanceId instanceId)
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("instance_id", instanceId.Value);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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

    private static WorkflowInstanceSnapshot SagaSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Compensated) with
        {
            SagaAudits =
            [
                new SagaAuditScopeSnapshot
                {
                    ScopeId = "checkout",
                    Outcome = WorkflowStatus.Compensated,
                    ForwardActions =
                    [
                        new SagaForwardActionSnapshot
                        {
                            ScopeId = "checkout",
                            ActionKey = "reserve",
                            CompensationKey = "release",
                            CompletedAt = Timestamp(2)
                        }
                    ],
                    CompensationActions =
                    [
                        new SagaCompensationActionSnapshot
                        {
                            ScopeId = "checkout",
                            ActionKey = "release",
                            Order = 0,
                            StartedAt = Timestamp(3),
                            CompletedAt = Timestamp(4),
                            Status = SagaCompensationActionStatus.Completed
                        }
                    ]
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

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
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
