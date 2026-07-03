using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait("Category", "Certification")]
public sealed class SqlServerRetentionCertificationTests : RetentionCertificationTests, IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();
    private SqlServerWorkflowStore? store;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        store = new SqlServerWorkflowStore(container.GetConnectionString());
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

    protected override IRetentionCertificationFixture CreateFixture()
    {
        return new SqlServerRetentionCertificationFixture(
            store ?? throw new InvalidOperationException("SQL Server retention store is not initialized."));
    }

    private sealed class SqlServerRetentionCertificationFixture(SqlServerWorkflowStore store)
        : IRetentionCertificationFixture
    {
        public IWorkflowEventStore EventStore => store;

        public IWorkflowOutboxStore OutboxStore => store;

        public IWorkflowProjectionStore ProjectionStore => store;

        public IWorkflowRetentionStore RetentionStore => store;

        public ITimerScheduler TimerScheduler => store;
    }
}
