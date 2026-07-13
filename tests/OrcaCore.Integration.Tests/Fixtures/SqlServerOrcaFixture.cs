using Microsoft.Data.SqlClient;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.SqlServer;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Integration.Tests.Fixtures;

public sealed class SqlServerOrcaFixture : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await using var store = await CreateStoreAsync();
        await store.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var store = await CreateStoreAsync();
        await store.DisposeAsync();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            delete from dbo.orcacore_resource_audit;
            delete from dbo.orcacore_resource_expired_tickets;
            delete from dbo.orcacore_resource_waiters;
            delete from dbo.orcacore_resource_tickets;
            delete from dbo.orcacore_resource_pools;
            delete from dbo.orcacore_active_wait_projections;
            delete from dbo.orcacore_timers;
            delete from dbo.orcacore_outbox;
            delete from dbo.orcacore_start_idempotency;
            delete from dbo.orcacore_inbox;
            delete from dbo.orcacore_checkpoints;
            delete from dbo.orcacore_instance_projections;
            delete from dbo.orcacore_events;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SqlServerWorkflowStore> CreateStoreAsync()
    {
        var store = new SqlServerWorkflowStore(ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    public async Task<DurableCommandProcessor> CreateProcessorAsync(SqlServerWorkflowStore? store = null)
    {
        store ??= await CreateStoreAsync();
        return new DurableCommandProcessor(store);
    }
}

[CollectionDefinition(nameof(SqlServerCollection), DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerOrcaFixture>;
