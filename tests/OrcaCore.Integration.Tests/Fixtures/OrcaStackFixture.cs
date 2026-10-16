using Xunit;

namespace OrcaCore.Integration.Tests.Fixtures;

public sealed class OrcaStackFixture : IAsyncLifetime
{
    public PostgreSqlOrcaFixture PostgreSql { get; } = new();
    public RabbitMqOrcaFixture RabbitMq { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.InitializeAsync();
        await RabbitMq.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await RabbitMq.DisposeAsync();
        await PostgreSql.DisposeAsync();
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await PostgreSql.ResetAsync(cancellationToken);
        await RabbitMq.PurgeQueueAsync(cancellationToken);
    }
}

[CollectionDefinition(nameof(OrcaStackCollection), DisableParallelization = true)]
public sealed class OrcaStackCollection : ICollectionFixture<OrcaStackFixture>;

[CollectionDefinition(nameof(MultiNodeCollection), DisableParallelization = true)]
public sealed class MultiNodeCollection : ICollectionFixture<OrcaStackFixture>;
