using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.SqlServer;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait(Traits.Container, "SqlServer")]
public sealed class SqlServerProviderCertificationTests : ContinueAsNewCertificationTests, IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();
    private SqlServerWorkflowStore? certificationStore;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        certificationStore = new SqlServerWorkflowStore(container.GetConnectionString());
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
        return new SqlServerProviderCertificationFixture(
            certificationStore ?? throw new InvalidOperationException("SQL Server certification store is not initialized."));
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
