using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

public sealed class PostgreSqlRetentionCertificationTests : RetentionCertificationTests, IAsyncLifetime
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

    protected override IRetentionCertificationFixture CreateFixture()
    {
        return new PostgreSqlRetentionCertificationFixture(
            certificationStore ?? throw new InvalidOperationException("PostgreSQL certification store is not initialized."));
    }

    private sealed class PostgreSqlRetentionCertificationFixture(PostgreSqlWorkflowStore store)
        : IRetentionCertificationFixture
    {
        public IWorkflowEventStore EventStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;

        public IWorkflowRetentionStore RetentionStore => store;
    }
}
