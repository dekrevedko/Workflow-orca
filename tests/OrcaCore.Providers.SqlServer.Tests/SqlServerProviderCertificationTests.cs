using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerProviderCertificationTests : ContinueAsNewCertificationTests, IAsyncLifetime
{
    private readonly SqlServerContainerFixture container;
    private SqlServerWorkflowStore? certificationStore;
    private SqlServerStateDocumentStore? certificationDocuments;

    public SqlServerProviderCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync()
    {
        var documents = await container.CreateDocumentsAsync();
        certificationDocuments = documents;
        certificationStore = new SqlServerWorkflowStore(documents, TimeProvider.System);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IProviderCertificationFixture CreateFixture() =>
        new SqlServerProviderCertificationFixture(
            certificationStore
            ?? throw new InvalidOperationException("SQL Server certification store is not initialized."));

    [Fact]
    public Task ResourceResize_GrantsEligiblePartitionQueue()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DurableResourceGrantRaceCertification.ResizeQueueAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            typeof(SqlServerDurableProviderOptions));
    }

    [Theory]
    [InlineData(DurableResourceGrantRaceCertification.ResizeRace.BlockedHead)]
    [InlineData(DurableResourceGrantRaceCertification.ResizeRace.OwnerCancels)]
    [InlineData(DurableResourceGrantRaceCertification.ResizeRace.HolderReleases)]
    public Task ResourceResize_FifoRace(DurableResourceGrantRaceCertification.ResizeRace schedule)
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DurableResourceGrantRaceCertification.ResizeRaceAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            typeof(SqlServerDurableProviderOptions), schedule);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ResourceGrant_ActiveWinnerRetainsTicket(bool competingAppend)
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DurableResourceGrantRaceCertification.RunAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            typeof(SqlServerDurableProviderOptions), competingAppend);
    }

    [Theory]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.ResumeReserved)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.CloseReserved)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.CloseEmpty)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.CloseJournalled)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.ResumePromoted)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.CloseQueued)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.CancelEmpty)]
    [InlineData(DurableResourceGrantRaceCertification.RecoveryCase.ReceiptReplay)]
    public Task ResourceGrant_Recovery(DurableResourceGrantRaceCertification.RecoveryCase schedule)
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DurableResourceGrantRaceCertification.RecoverAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            typeof(SqlServerDurableProviderOptions), schedule);
    }

    [Theory]
    [ClassData<DurableEventTimeoutCertification.Schedules>]
    public Task DurableEventTimeout_ReusedKey(DurableEventTimeoutCertification.Schedule schedule) =>
        DurableEventTimeoutCertification.RunAsync(CreateFixture(), certificationStore!, schedule);

    [Theory]
    [InlineData("key")]
    [InlineData("json")]
    public Task DagFacade_CanonicalInputIntegrity(string corruption)
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DagRunCertification.FacadeCanonicalIntegrityAsync(() => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)), corruption, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagFacade_RealProcessRestart() => DagProcessRestartCertification.RunAsync(
        "sqlserver", container.ConnectionString, SqlServerContainerFixture.CreateSchemaName(), TestContext.Current.CancellationToken);

    [Fact]
    public Task DagFacade_BindingAndLosslessness()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DagRunCertification.FacadeBindingsAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagFacade_LevelTriggeredFaults()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DagRunCertification.FacadeFaultsAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-322")]
    public async Task PositionOnlyCommitRejectsASecondHostAtTheOldVersion(bool fiberFailure)
    {
        var documents = await container.CreateDocumentsAsync(certificationDocuments!.Schema);
        var other = new SqlServerWorkflowStore(documents, TimeProvider.System);
        await RuntimePositionCertification.PositionOnlyCommitRejectsASecondHostAtTheOldVersion(
            certificationStore!, other, fiberFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-323")]
    public async Task InterleavedCheckpointLoadMustNotCommitPublishTwice(bool afterTail)
    {
        var documents = await container.CreateDocumentsAsync(certificationDocuments!.Schema);
        var other = new SqlServerWorkflowStore(documents, TimeProvider.System);
        await RuntimePositionCertification.InterleavedCheckpointLoadMustNotCommitPublishTwice(
            certificationStore!, other, certificationStore!, afterTail);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task DagScheduler_CachedNoReadyRunRecoversRemoteJoinAfterLostWake(bool restartScheduler)
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        return DagRunCertification.RemoteJoinAfterLostWakeAsync(
            () => new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(new(container.ConnectionString, schema)),
            restartScheduler, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagScheduler_ProgressesTwoRunsAndCancelsBlockedMapper() =>
        DagRunCertification.ShippedScheduler_ProgressesTwoRunsAndCancelsTheBlockedMapperAsync(
            new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(
                new(container.ConnectionString, SqlServerContainerFixture.CreateSchemaName())), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public Task DagRetainedChild_DoesNotStarveHealthyRun(int batchSize) =>
        DagRunCertification.RetainedChildDoesNotStarveHealthyRunAsync(
            new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(
                new(container.ConnectionString, SqlServerContainerFixture.CreateSchemaName())), batchSize, TestContext.Current.CancellationToken);

    [Fact]
    public Task DagCreationAndReplay_PersistAnIndependentAggregate() =>
        DagRunCertification.CreationAndReplayAsync(certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public Task DagFencedAppend_AtomicallyPersistsInputsAndAddressedNotices() =>
        DagRunCertification.FencedAppendAndObserversAsync(certificationStore!, certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public async Task DagLeaseQueue_UsesFifoAndAnAbsoluteTurnDeadline()
    {
        var time = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        certificationStore!.DagTransactionClock.TestUtcNow = () => time;
        await DagRunCertification.LeaseQueueAsync(certificationStore, () => time, duration => time += duration, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DagDeadWaiter_ExpiresWithoutBlockingTheLiveQueue()
    {
        var time = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        certificationStore!.DagTransactionClock.TestUtcNow = () => time;
        await DagRunCertification.DeadWaiterExpiresAsync(certificationStore, duration => time += duration, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagTurnPurpose_RejectsForeignRecordsBeforeStorage() =>
        DagRunCertification.TurnPurposeRejectsForeignRecordsAsync(certificationStore!, certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public Task DagLastNode_RequiresAtomicRunClosure() =>
        DagRunCertification.LastNodeRequiresAtomicRunClosureAsync(certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public Task DagFailureClosure_PreservesIndependentWorkAndDerivesTerminalOutcome() =>
        DagRunCertification.FailureClosureAndTerminalAsync(certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public async Task DagObservers_CleanExpiredNoticesButRetainClaimsPoisonAndBusinessWork()
    {
        var time = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        certificationStore!.DagTransactionClock.TestUtcNow = () => time;
        await DagRunCertification.ObserverCleanupAsync(certificationStore, certificationStore, () => time, duration => time += duration, TestContext.Current.CancellationToken);
    }


    [Theory]
    [InlineData(DagRunCertification.WakeOrder.CursorFirst)]
    [InlineData(DagRunCertification.WakeOrder.AppendBeforeCursor)]
    [InlineData(DagRunCertification.WakeOrder.AckFirst)]
    [InlineData(DagRunCertification.WakeOrder.AppendBeforeAck)]
    public async Task DagFirstUsableWake_OperationsSerialize(DagRunCertification.WakeOrder order)
    {
        var token = TestContext.Current.CancellationToken;
        var tag = Guid.NewGuid().ToString("N");
        var barrierName = "wake-barrier-" + tag;
        var readerName = "wake-reader-" + tag;
        var writerName = "wake-writer-" + tag;

        string Connection(string name) => new SqlConnectionStringBuilder(container.ConnectionString) { ApplicationName = name }.ConnectionString;
        var schema = certificationDocuments!.Schema;
        var writerDocs = new SqlServerStateDocumentStore(Connection(writerName), schema);
        var readerDocs = new SqlServerStateDocumentStore(Connection(readerName), schema);
        await writerDocs.InitializeAsync(token);
        await readerDocs.InitializeAsync(token);
        var writer = new SqlServerWorkflowStore(writerDocs, TimeProvider.System);
        var reader = new SqlServerWorkflowStore(readerDocs, TimeProvider.System);
        await using var barrier = new SqlConnection(Connection(barrierName));
        await using var monitor = new SqlConnection(container.ConnectionString);
        var schemaPrefix = "[" + schema.Replace("]", "]]", StringComparison.Ordinal) + "].";
        var lockRow = $"select run_id from {schemaPrefix}[dag_runs] with (updlock,holdlock) where run_id=@id";
        var subscriptions = schemaPrefix + "[dag_run_subscriptions]";
        const string detectBlock = """
            select 1 from sys.dm_exec_requests r
            join sys.dm_exec_sessions waiter on waiter.session_id=r.session_id
            join sys.dm_exec_sessions holder on holder.session_id=r.blocking_session_id
            where waiter.program_name=@waiter and holder.program_name=@holder
            """;

        var now = DagRunCertification.WakeTestTime();
        writer.DagTransactionClock.TestUtcNow = reader.DagTransactionClock.TestUtcNow = () => now;
        var readerFirst = order is DagRunCertification.WakeOrder.CursorFirst or DagRunCertification.WakeOrder.AckFirst;
        Task Block(string statement, Guid id, Func<Task> first, Func<Task> second, CancellationToken ct) =>
            DagRunCertification.ExerciseRelationalWakeOrderAsync(barrier, monitor, statement, id, detectBlock,
                barrierName, readerFirst ? readerName : writerName, readerFirst ? writerName : readerName, first, second, ct);
        if (order is DagRunCertification.WakeOrder.CursorFirst or DagRunCertification.WakeOrder.AppendBeforeCursor)
        {
            await DagRunCertification.WakeCursorRaceAsync(writer, reader, readerFirst, () => now, duration => now += duration,
                async (sub, first, second, ct) =>
                {
                    await Block(lockRow, sub.RunId, first, second, ct);
                    await DagRunCertification.AssertWakeCursorStoredAsync(monitor, subscriptions, sub.SubscriptionId,
                        readerFirst ? 3 : 1, now.AddSeconds(readerFirst ? 9 : 30), ct);
                }, token);
            return;
        }
        // Both settlement and append acquire the same document application lock before opening this state row.
        var holdDocument = $"select @id from {schemaPrefix}[orcacore_provider_state] with (updlock,holdlock) where state_key='workflow'";
        await DagRunCertification.WakeSettlementRaceAsync(writer, reader, readerFirst, now,
            (wake, _, first, second, ct) => Block(holdDocument, wake, first, second, ct), token);
    }

    [Fact]
    public Task DagFirstUsableWake_DeliveryRetainsPayloadClaimLeaseAndHistory()
    {
        var now = DagRunCertification.WakeTestTime();
        var store = certificationStore!;
        store.DagTransactionClock.TestUtcNow = () => now;
        return DagRunCertification.WakeDeliveryAsync(store, () => now, duration => now += duration, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DagFirstUsableWake_CollidingCandidateRollsBack(bool previouslyStored) =>
        DagRunCertification.WakeCollisionAsync(certificationStore!, previouslyStored, TestContext.Current.CancellationToken);

    [Fact]
    public async Task DagConcurrentLeaseRequests_SerializeAcrossStoreInstances()
    {
        var documents = new SqlServerStateDocumentStore(container.ConnectionString, certificationDocuments!.Schema);
        var other = new SqlServerWorkflowStore(documents,TimeProvider.System);
        await DagRunCertification.ConcurrentLeasesAsync(certificationStore!, other, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task DagCancellationAndIntegrity_PreserveAdmissionsAndDeriveCancelledTerminal() =>
        DagRunCertification.CancellationAndIntegrityAsync(certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    public Task DagAndWorkflowIdentities_CannotOwnTheSameGuid() =>
        DagRunCertification.CrossAggregateIdentityAsync(certificationStore!, certificationStore!, TestContext.Current.CancellationToken);

    [Fact]
    [Trait("AC", "AC-108")]
    public Task DefinitionFanoutInbox_UsesAtomicStablePerTargetOwnership() =>
        DefinitionFanoutInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public Task StartOrDeliverInbox_UsesAtomicInputBoundIntentOwnership() =>
        StartOrDeliverInboxCertification.RunAsync(CreateFixture());

    [Fact]
    public Task OperationalStatisticsAndMaintenance_AreProviderAuthoritativeAndReferenceSafe()
    {
        var store = certificationStore
            ?? throw new InvalidOperationException("SQL Server certification store is not initialized.");
        return OperationalMaintenanceCertification.RunAsync(
            store,
            store,
            store,
            store,
            store,
            store,
            OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics.SqlServerProviderName,
            TestContext.Current.CancellationToken);
    }

    private sealed class SqlServerProviderCertificationFixture(SqlServerWorkflowStore store)
        : IProviderCertificationFixture
    {
        public IWorkflowEventStore EventStore => store;

        public IWorkflowInboxStore InboxStore => store;

        public IWorkflowStartIdempotencyStore StartIdempotencyStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;
    }
}
