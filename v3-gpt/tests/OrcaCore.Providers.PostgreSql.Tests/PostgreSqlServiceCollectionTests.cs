using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
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
