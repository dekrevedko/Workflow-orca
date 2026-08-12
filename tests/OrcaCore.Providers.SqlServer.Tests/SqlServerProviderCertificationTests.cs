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

    public SqlServerProviderCertificationTests(SqlServerContainerFixture container)
    {
        this.container = container;
    }

    public async ValueTask InitializeAsync()
    {
        var documents = await container.CreateDocumentsAsync();
        certificationStore = new SqlServerWorkflowStore(documents, TimeProvider.System);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected override IProviderCertificationFixture CreateFixture() =>
        new SqlServerProviderCertificationFixture(
            certificationStore
            ?? throw new InvalidOperationException("SQL Server certification store is not initialized."));

    [Fact]
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
