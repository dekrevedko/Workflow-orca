using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Providers.SqlServer.Tests;

public sealed class SqlServerServiceCollectionTests
{
    [Fact]
    public async Task AddOrcaCoreSqlServer_RegistersDurableProviderPortsResourcePoolsAndRetentionStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEventStore, StubEventStore>();

        services.AddOrcaCoreSqlServer("Server=localhost;Database=orca;User Id=orca;Password=orca;TrustServerCertificate=True");

        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWorkflowEventStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowInboxStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowStartIdempotencyStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowOutboxStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowProjectionStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<ITimerScheduler>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowRetentionStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IResourcePoolStore>().Should().BeOfType<SqlServerWorkflowStore>();
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

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
