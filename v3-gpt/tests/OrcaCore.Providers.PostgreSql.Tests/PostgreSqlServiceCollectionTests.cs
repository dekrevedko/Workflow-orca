using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

public sealed class PostgreSqlServiceCollectionTests
{
    [Fact]
    public async Task AddOrcaCorePostgreSql_RegistersDurableProviderPortsAndRetentionStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEventStore, StubEventStore>();

        services.AddOrcaCorePostgreSql("Host=localhost;Database=orca;Username=orca;Password=orca");

        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWorkflowEventStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IWorkflowInboxStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IWorkflowStartIdempotencyStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IWorkflowOutboxStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IWorkflowProjectionStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<ITimerScheduler>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IWorkflowRetentionStore>().Should().BeOfType<PostgreSqlWorkflowStore>();
        provider.GetRequiredService<IResourcePoolStore>().Should().BeOfType<PostgreSqlResourcePoolStore>();
    }

    [Fact]
    public async Task WorkflowStore_WithBorrowedDataSource_DoesNotDisposeDataSource()
    {
        await using var dataSource = CreateUnreachableDataSource();
        await using var store = new PostgreSqlWorkflowStore(dataSource);

        await store.DisposeAsync();

        await AssertBorrowedDataSourceWasNotDisposedAsync(dataSource);
    }

    [Fact]
    public async Task ResourcePoolStore_WithBorrowedDataSource_DoesNotDisposeDataSource()
    {
        await using var dataSource = CreateUnreachableDataSource();
        await using var store = new PostgreSqlResourcePoolStore(dataSource);

        await store.DisposeAsync();

        await AssertBorrowedDataSourceWasNotDisposedAsync(dataSource);
    }

    [Fact]
    public async Task AddOrcaCorePostgreSql_WithBorrowedDataSource_DoesNotDisposeDataSourceOnProviderDispose()
    {
        await using var dataSource = CreateUnreachableDataSource();
        var services = new ServiceCollection();

        services.AddOrcaCorePostgreSql(dataSource);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PostgreSqlWorkflowStore>().Should().NotBeNull();
        provider.GetRequiredService<PostgreSqlResourcePoolStore>().Should().NotBeNull();

        await provider.DisposeAsync();

        await AssertBorrowedDataSourceWasNotDisposedAsync(dataSource);
    }

    private static NpgsqlDataSource CreateUnreachableDataSource()
    {
        return NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=orca;Username=orca;Password=orca;Timeout=1;Command Timeout=1");
    }

    private static async Task AssertBorrowedDataSourceWasNotDisposedAsync(NpgsqlDataSource dataSource)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var exception = await Record.ExceptionAsync(async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(timeout.Token);
        });

        exception.Should().NotBeOfType<ObjectDisposedException>();
    }

    private sealed class StubEventStore : IWorkflowEventStore
    {
        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
