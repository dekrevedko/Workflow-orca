using Npgsql;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.PostgreSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Integration.Tests.Fixtures;

public sealed class PostgreSqlOrcaFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await using var store = await CreateStoreAsync();
        await using var pools = await CreatePoolStoreAsync();
        await store.DisposeAsync();
        await pools.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var store = await CreateStoreAsync();
        await using var pools = await CreatePoolStoreAsync();
        await store.DisposeAsync();
        await pools.DisposeAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            truncate table
                orcacore_history_projections,
                orcacore_active_wait_projections,
                orcacore_timers,
                orcacore_outbox,
                orcacore_inbox,
                orcacore_checkpoints,
                orcacore_events,
                orcacore_instance_projections,
                orcacore_resource_audit,
                orcacore_resource_expired_tickets,
                orcacore_resource_waiters,
                orcacore_resource_tickets,
                orcacore_resource_pools
            restart identity cascade;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PostgreSqlWorkflowStore> CreateStoreAsync()
    {
        var store = new PostgreSqlWorkflowStore(ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    public async Task<PostgreSqlResourcePoolStore> CreatePoolStoreAsync()
    {
        var store = new PostgreSqlResourcePoolStore(ConnectionString);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    public async Task<DurableCommandProcessor> CreateProcessorAsync(
        PostgreSqlWorkflowStore? store = null,
        PostgreSqlResourcePoolStore? pools = null)
    {
        store ??= await CreateStoreAsync();
        if (pools is not null)
        {
            return new DurableCommandProcessor(store, pools);
        }

        return new DurableCommandProcessor(store);
    }

    public DurableManagement CreateManagement(PostgreSqlWorkflowStore store, PostgreSqlResourcePoolStore? pools = null)
    {
        return pools is null
            ? new DurableManagement(store, eventStore: store)
            : new DurableManagement(store, pools, store);
    }
}

[CollectionDefinition(nameof(PostgreSqlCollection), DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlOrcaFixture>;
