using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

public sealed class PostgreSqlTimerSchedulerCertificationTests : TimerSchedulerCertificationTests, IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();
    private PostgreSqlWorkflowStore? store;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        store = new PostgreSqlWorkflowStore(container.GetConnectionString());
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

    protected override ITimerScheduler CreateTimerScheduler()
    {
        return store ?? throw new InvalidOperationException("PostgreSQL timer scheduler is not initialized.");
    }
}
