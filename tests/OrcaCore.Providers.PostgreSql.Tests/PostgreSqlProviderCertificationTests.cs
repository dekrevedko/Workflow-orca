using System.Security.Cryptography;
using AwesomeAssertions;
using Npgsql;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;
using ProjectionActiveWaitSnapshot = OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot;
using ProjectionWorkflowInstanceSnapshot = OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;

namespace OrcaCore.Providers.PostgreSql.Tests;

[Trait(Traits.Container, "PostgreSql")]
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
        certificationStore = new PostgreSqlWorkflowStore(
            container.GetConnectionString(),
            new FixedTimeProvider(MigrationAppliedAt()));
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
    public Task DefinitionFanoutInbox_UsesAtomicStablePerTargetOwnership() =>
        DefinitionFanoutInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public Task StartOrDeliverInbox_UsesAtomicInputBoundIntentOwnership() =>
        StartOrDeliverInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public async Task DefinitionFanoutInbox_RestartRetainsEnvelopeAndExactMembership()
    {
        var store = certificationStore ??
            throw new InvalidOperationException("PostgreSQL certification store is not initialized.");
        var definitionId = DefinitionId.New();
        var firstInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var secondInstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var envelope = new DurableEventEnvelope
        {
            EventId = eventId,
            EventName = "definition-fanout-restart",
            EventContractVersion = 3,
            CorrelationId = CorrelationId.Create("definition-fanout-restart"),
            OccurredAt = MigrationAppliedAt(),
            Route = new DurableEventRouteEnvelope
            {
                Kind = "definition-fanout",
                DefinitionId = definitionId
            }
        };
        await store.ApplyAsync(
            [
                FanoutSnapshot(firstInstanceId, definitionId, DefinitionVersion.Initial),
                FanoutSnapshot(secondInstanceId, definitionId, new DefinitionVersion(2))
            ],
            TestContext.Current.CancellationToken);
        var request = new InboxDefinitionFanoutAcceptance(
            new InboxAcceptance(envelope, "definition-fanout-restart-envelope", envelope.OccurredAt),
            definitionId,
            MaximumTargetCount: 2);
        (await store.AcceptDefinitionFanoutAsync(request, TestContext.Current.CancellationToken))
            .Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);

        await using var replacement = await CreateStoreAsync();
        var targets = await replacement.ListDefinitionFanoutTargetsAsync(
            eventId,
            TestContext.Current.CancellationToken);
        var duplicate = await replacement.AcceptDefinitionFanoutAsync(
            request,
            TestContext.Current.CancellationToken);

        targets.Select(target => target.InstanceId).Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);
        targets.Should().OnlyContain(target => target.EnvelopeFingerprint == "definition-fanout-restart-envelope");
        duplicate.Disposition.Should().Be(InboxAcceptanceCommitDisposition.Duplicate);
        duplicate.DefinitionFanoutTargets.Should().BeEquivalentTo([firstInstanceId, secondInstanceId]);
    }

    [Fact]
    public async Task StartOrDeliverInbox_RestartRetainsIntentThenMaterializesAllBoundEvents()
    {
        var store = certificationStore ??
            throw new InvalidOperationException("PostgreSQL certification store is not initialized.");
        var definitionId = DefinitionId.New();
        var definitionVersion = new DefinitionVersion(4);
        var startKey = $"start-restart-{Guid.CreateVersion7():N}";
        var input = "restart-workflow-input"u8.ToArray();
        var inputFingerprint = Convert.ToHexString(SHA256.HashData(input));
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
        var envelope = new DurableEventEnvelope
        {
            EventId = eventId,
            EventName = "start-or-deliver-restart",
            EventContractVersion = 2,
            CorrelationId = CorrelationId.Create("start-or-deliver-restart"),
            OccurredAt = MigrationAppliedAt(),
            PayloadContentType = "application/vnd.orcacore.fixed+json;v=1",
            Payload = "restart-event-payload"u8.ToArray(),
            Route = new DurableEventRouteEnvelope
            {
                Kind = "start-or-deliver",
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                StartIdempotencyKey = startKey,
                WorkflowInputContentType = "application/vnd.orcacore.fixed+json;v=1",
                WorkflowInputPayload = input
            }
        };
        var request = new InboxStartOrDeliverAcceptance(
            new InboxAcceptance(envelope, "start-or-deliver-restart-envelope", envelope.OccurredAt),
            definitionId,
            definitionVersion,
            startKey,
            envelope.Route.WorkflowInputContentType,
            [.. input],
            inputFingerprint);
        (await store.AcceptStartOrDeliverAsync(request, TestContext.Current.CancellationToken))
            .Disposition.Should().Be(InboxAcceptanceCommitDisposition.Accepted);

        await using var replacement = await CreateStoreAsync();
        (await replacement.GetStartIntentAsync(startKey, TestContext.Current.CancellationToken))
            .Value.State.Should().Be(InboxStartIntentState.Pending);
        (await replacement.GetByEventIdAsync(eventId, TestContext.Current.CancellationToken))
            .Value.InstanceId.Should().BeNull();

        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        (await replacement.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                StartIdempotencyOperations =
                [
                    new StartIdempotencyWrite(
                        startKey,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        "restart-definition-fingerprint",
                        inputFingerprint)
                ]
            },
            TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();

        await using var secondReplacement = await CreateStoreAsync();
        var materialized = await secondReplacement.GetStartIntentAsync(
            startKey,
            TestContext.Current.CancellationToken);
        materialized.Value.State.Should().Be(InboxStartIntentState.Materialized);
        materialized.Value.InstanceId.Should().Be(instanceId);
        (await secondReplacement.GetAsync(instanceId, eventId, TestContext.Current.CancellationToken))
            .Value.Route!.Kind.Should().Be("direct");
    }

    [Fact]
    public void ActiveWaitQuery_ImplementsTheVersionAwareProviderOverload()
    {
        var projectionStore = certificationStore ??
            throw new InvalidOperationException("PostgreSQL certification store is not initialized.");

        projectionStore.GetType().GetMethod(
                nameof(IWorkflowProjectionStore.FindActiveWaitsAsync),
                [
                    typeof(DefinitionId),
                    typeof(EventName),
                    typeof(EventContractVersion),
                    typeof(CorrelationId),
                    typeof(CancellationToken)
                ])
            .Should().NotBeNull(
                "the PostgreSQL provider must push version identity into its indexed SQL query");
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
        var ownershipCompatibilityMigrationCount = await ScalarAsync<long>(
            "select count(*) from orcacore_schema_migrations where migration_id = @migration_id;",
            "007_resource_ownership");
        var fanoutMigrationId = await ScalarAsync<string>(
            "select migration_id from orcacore_schema_migrations where migration_id = @migration_id;",
            "011_definition_fanout_inbox");
        var pendingStartMigrationId = await ScalarAsync<string>(
            "select migration_id from orcacore_schema_migrations where migration_id = @migration_id;",
            "012_pending_start_intents");

        initialMigrationId.Should().Be("001_initial");
        leaseMigrationId.Should().Be("002_claim_leases");
        startIdempotencyMigrationId.Should().Be("003_start_idempotency");
        ownershipCompatibilityMigrationCount.Should().Be(0);
        fanoutMigrationId.Should().Be("011_definition_fanout_inbox");
        pendingStartMigrationId.Should().Be("012_pending_start_intents");
    }

    [Fact]
    public async Task InitializeAsync_UsesTimeProviderForMigrationJournalTimestamps()
    {
        await using var connection = new NpgsqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select count(*)
            from orcacore_schema_migrations
            where applied_at = @applied_at;
            """,
            connection);
        command.Parameters.AddWithValue("applied_at", MigrationAppliedAt());

        var count = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException());

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("AC", "AC-305")]
    public async Task PostgreSql_DuplicateEventsBeforeAndAfterRestartDedup()
    {
        var eventId = EventIdValue(100);
        var instanceId = InstanceIdValue(1);
        await using (var store = await CreateStoreAsync())
        {
            await store.AppendAsync(
                Batch(
                    instanceId,
                    StreamVersion.Empty,
                    inbox:
                    [
                        new InboxWrite(eventId, InboxRecordState.Applied)
                        {
                            EnvelopeFingerprint = "postgres-envelope"
                        }
                    ]),
                TestContext.Current.CancellationToken);
        }

        await using var restarted = await CreateStoreAsync();
        var inbox = await restarted.GetAsync(
            instanceId,
            eventId,
            TestContext.Current.CancellationToken);
        await restarted.AppendAsync(
            Batch(
                instanceId,
                new StreamVersion(1),
                inbox: [new InboxWrite(eventId, InboxRecordState.Received)]),
            TestContext.Current.CancellationToken);
        var afterDuplicate = await restarted.GetAsync(
            instanceId,
            eventId,
            TestContext.Current.CancellationToken);

        inbox.Value.EnvelopeFingerprint.Should().Be("postgres-envelope");
        inbox.Value.State.Should().Be(InboxRecordState.Applied);
        afterDuplicate.Value.EnvelopeFingerprint.Should().Be("postgres-envelope");
        afterDuplicate.Value.State.Should().Be(InboxRecordState.Applied);
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
                            DefinitionVersion.Initial,
                            "definition-fingerprint-1",
                            "input-fingerprint-1")
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
        existing.Value.DefinitionFingerprint.Should().Be("definition-fingerprint-1");
        existing.Value.InputFingerprint.Should().Be("input-fingerprint-1");
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
                        DefinitionVersion.Initial,
                        "definition-fingerprint-1",
                        "input-fingerprint-1")
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
                        new DefinitionVersion(2),
                        "definition-fingerprint-2",
                        "input-fingerprint-2")
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
    public async Task PostgreSql_GetAsync_UsesTheExactInstanceIdentity()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(54);
        var otherInstanceId = InstanceIdValue(154);
        await store.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = RunningSnapshot(instanceId)
                },
                new ProjectionWrite(otherInstanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = RunningSnapshot(otherInstanceId)
                }
            ],
            TestContext.Current.CancellationToken);

        var snapshot = await store.GetAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        snapshot.Value.InstanceId.Should().Be(instanceId);
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
            InstanceId.Parse(request.TimerId.Value.ToString()));
        var secondClaim = await store.ClaimDueAsync(Timestamp(10), 10, TestContext.Current.CancellationToken);

        await store.CompleteAsync(request.TimerId, TestContext.Current.CancellationToken);
        var completedCount = await ScalarAsync<long>(
            "select count(*) from orcacore_timers where timer_id = @instance_id;",
            InstanceId.Parse(request.TimerId.Value.ToString()));

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

        var activePurge = await store.PurgeForRetentionAsync(
            activeInstanceId,
            TestContext.Current.CancellationToken);
        var claimedPurge = await store.PurgeForRetentionAsync(
            claimedInstanceId,
            TestContext.Current.CancellationToken);
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

        var results = await store.FindActiveWaitsAsync(
            definitionId: null,
            EventName.Create("Approved"),
            CorrelationId.Create("order-1"),
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PostgreSql_CurrentWorkflowEventsAndSummaryProjectionRoundTrip()
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
                    new WorkflowStepCompletedEvent
                    {
                        EventId = EventIdValue(101),
                        InstanceId = instanceId,
                        CommandId = CommandIdValue(2),
                        CausationId = CausationIdValue(2),
                        OccurredAt = Timestamp(2),
                        StepPath = "root/1"
                    }
                ],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = CompletedSnapshot(instanceId)
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var projection = await store.GetAsync(
            instanceId,
            TestContext.Current.CancellationToken);

        tail.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle()
            .Which.StepPath.Should().Be("root/1");
        projection.Value.Status.Should().Be(WorkflowInstanceStatus.Completed);
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
        ProjectionWorkflowInstanceSnapshot? projection = null)
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

    private static ProjectionWorkflowInstanceSnapshot RunningSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Running);
    }

    private static ProjectionWrite FanoutSnapshot(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion) =>
        new(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new ProjectionWorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                Status = WorkflowStatus.Running,
                CreatedAt = MigrationAppliedAt(),
                UpdatedAt = MigrationAppliedAt()
            }
        };

    private static ProjectionWorkflowInstanceSnapshot CompletedSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Completed);
    }

    private static ProjectionWorkflowInstanceSnapshot WaitingSnapshot(InstanceId instanceId)
    {
        return Snapshot(instanceId, WorkflowStatus.Waiting) with
        {
            ActiveWaits =
            [
                new ProjectionActiveWaitSnapshot
                {
                    WaitId = WaitIdValue(1),
                    EventName = "Approved",
                    CorrelationId = CorrelationId.Create("order-1"),
                    RegisteredAt = Timestamp(2),
                    Status = "Active",
                    Mode = "Resident"
                }
            ]
        };
    }

    private static ProjectionWorkflowInstanceSnapshot Snapshot(InstanceId instanceId, WorkflowStatus status)
    {
        return new ProjectionWorkflowInstanceSnapshot
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
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

    private static DateTimeOffset MigrationAppliedAt()
    {
        return new DateTimeOffset(2026, 7, 4, 11, 30, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
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

    private static OutboxRecordId OutboxRecordIdValue(int value)
    {
        return new OutboxRecordId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
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

        public IWorkflowStartIdempotencyStore StartIdempotencyStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;
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
