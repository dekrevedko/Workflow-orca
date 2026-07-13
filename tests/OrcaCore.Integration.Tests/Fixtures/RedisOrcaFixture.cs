using OrcaCore.Providers.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace OrcaCore.Integration.Tests.Fixtures;

public sealed class RedisOrcaFixture : IAsyncLifetime
{
    private readonly RedisContainer container = new RedisBuilder("redis:7-alpine")
        .Build();

    private ConnectionMultiplexer? connection;

    public string ConnectionString => container.GetConnectionString();

    public IDatabase Database =>
        connection?.GetDatabase() ?? throw new InvalidOperationException("Redis fixture is not initialized.");

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        connection = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            connection.Dispose();
        }

        await container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await Database.ExecuteAsync("FLUSHDB");
    }

    public RedisProjectionStore CreateProjectionStore()
    {
        return new RedisProjectionStore(Database);
    }
}

[CollectionDefinition(nameof(RedisCollection), DisableParallelization = true)]
public sealed class RedisCollection : ICollectionFixture<RedisOrcaFixture>;
