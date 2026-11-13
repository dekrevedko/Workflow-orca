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
