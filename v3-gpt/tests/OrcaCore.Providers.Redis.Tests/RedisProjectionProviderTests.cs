using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.Redis;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace OrcaCore.Providers.Redis.Tests;

public sealed class RedisProjectionProviderTests
{
    [Fact]
    public async Task ListAsync_MetadataFilters_ReturnsMatchingSnapshotsWithoutPayloadDeserialization()
    {
        var store = new RedisProjectionStore();
        var matchingDefinition = DefinitionIdValue(1);
        var otherDefinition = DefinitionIdValue(2);
        await store.ApplyAsync(
            [
                Upsert(InstanceIdValue(1), matchingDefinition, WorkflowStatus.Running),
                Upsert(InstanceIdValue(2), matchingDefinition, WorkflowStatus.Completed),
                Upsert(InstanceIdValue(3), otherDefinition, WorkflowStatus.Running)
            ],
            TestContext.Current.CancellationToken);

        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery
            {
                DefinitionId = matchingDefinition,
                Status = WorkflowStatus.Running
            },
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.InstanceId.Should().Be(InstanceIdValue(1));
    }

    [Fact]
    public void ProviderProfile_UnsupportedEventStorePort_IsNotRegistered()
    {
        var profile = new RedisProviderProfile(new RedisProjectionStore());

        profile.ProjectionStore.Should().BeAssignableTo<IWorkflowProjectionStore>();
        profile.SupportsEventStore.Should().BeFalse();
        profile.EventStore.Should().BeNull();
    }

    [Fact]
    public void AddOrcaCoreRedisProjectionCache_RegistersProjectionStoreOnly()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowProjectionStore, StubProjectionStore>();

        services.AddOrcaCoreRedisProjectionCache(new RedisProjectionStore());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWorkflowProjectionStore>().Should().BeOfType<RedisProjectionStore>();
        provider.GetService<IWorkflowEventStore>().Should().BeNull();
    }

    [Fact]
    [Trait("Container", "Redis")]
    public async Task AdapterBackedStore_PersistsSnapshotsAcrossStoreInstances()
    {
        await using var container = new RedisBuilder("redis:7-alpine")
            .Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
        using var connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        var writer = new RedisProjectionStore(connection.GetDatabase());
        var reader = new RedisProjectionStore(connection.GetDatabase());
        var instanceId = InstanceIdValue(4);

        await writer.ApplyAsync(
            [Upsert(instanceId, DefinitionIdValue(4), WorkflowStatus.Waiting) with
            {
                InstanceSnapshot = Upsert(instanceId, DefinitionIdValue(4), WorkflowStatus.Waiting).InstanceSnapshot! with
                {
                    ActiveWaits =
                    [
                        new ActiveWaitSnapshot
                        {
                            WaitId = new WaitId(GuidValue(40)),
                            EventName = "Approved",
                            CorrelationId = new CorrelationId("order-4"),
                            RegisteredAt = Timestamp(2),
                            Status = "Active",
                            Mode = "Resident"
                        }
                    ]
                }
            }],
            TestContext.Current.CancellationToken);

        var snapshots = await reader.ListAsync(
            new WorkflowProjectionQuery
            {
                ActiveWaitEventName = "Approved",
                ActiveWaitCorrelationId = new CorrelationId("order-4")
            },
            TestContext.Current.CancellationToken);

        writer.UsesRedisAdapter.Should().BeTrue();
        snapshots.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
    }

    [Fact]
    [Trait("Container", "Redis")]
    public async Task AdapterBackedStore_UpdatedSnapshotMovesBetweenMetadataIndexes()
    {
        await using var container = new RedisBuilder("redis:7-alpine")
            .Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
        using var connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        var store = new RedisProjectionStore(connection.GetDatabase());
        var instanceId = InstanceIdValue(5);
        var oldDefinition = DefinitionIdValue(50);
        var newDefinition = DefinitionIdValue(51);

        await store.ApplyAsync(
            [Upsert(instanceId, oldDefinition, WorkflowStatus.Running)],
            TestContext.Current.CancellationToken);
        await store.ApplyAsync(
            [Upsert(instanceId, newDefinition, WorkflowStatus.Completed)],
            TestContext.Current.CancellationToken);

        var oldIndexResults = await store.ListAsync(
            new WorkflowProjectionQuery
            {
                DefinitionId = oldDefinition,
                Status = WorkflowStatus.Running
            },
            TestContext.Current.CancellationToken);
        var newIndexResults = await store.ListAsync(
            new WorkflowProjectionQuery
            {
                DefinitionId = newDefinition,
                Status = WorkflowStatus.Completed
            },
            TestContext.Current.CancellationToken);

        oldIndexResults.Should().BeEmpty();
        newIndexResults.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
    }

    [Fact]
    [Trait("Container", "Redis")]
    public async Task AdapterBackedStore_SkipsTamperedProjectionPayloads()
    {
        await using var container = new RedisBuilder("redis:7-alpine")
            .Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
        using var connection = await ConnectionMultiplexer.ConnectAsync(container.GetConnectionString());
        var database = connection.GetDatabase();
        var store = new RedisProjectionStore(database);
        var instanceId = InstanceIdValue(5);
        await store.ApplyAsync(
            [Upsert(instanceId, DefinitionIdValue(5), WorkflowStatus.Running)],
            TestContext.Current.CancellationToken);
        await database.StringSetAsync(
            $"orcacore:projection:instance:{instanceId.Value:N}",
            "orcacore:v1:not-base64:not-json");

        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);

        snapshots.Should().BeEmpty();
    }

    [Fact]
    public void RedisProjectionStore_SourceUsesSecondaryIndexesForFilteredListQueries()
    {
        var source = File.ReadAllText(FindRepoFile("src/OrcaCore.Providers.Redis/RedisProjectionStore.cs"));

        source.Should().Contain("CandidateIndexKeys");
        source.Should().Contain("SetCombineAsync");
        source.Should().Contain("DefinitionIndexKey");
        source.Should().Contain("StatusIndexKey");
    }

    [Fact]
    public void RedisProjectionStore_SourceUsesOptimisticTransactionForProjectionUpserts()
    {
        var source = File.ReadAllText(FindRepoFile("src/OrcaCore.Providers.Redis/RedisProjectionStore.cs"));

        source.Should().Contain("CreateTransaction");
        source.Should().Contain("Condition.StringEqual");
        source.Should().Contain("Condition.KeyNotExists");
        source.Should().Contain("ExecuteAsync");
    }

    private static ProjectionWrite Upsert(
        InstanceId instanceId,
        DefinitionId definitionId,
        WorkflowStatus status)
    {
        return new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = status,
                CreatedAt = Timestamp(1),
                UpdatedAt = Timestamp(1)
            }
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}'.");
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class StubProjectionStore : IWorkflowProjectionStore
    {
        public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<WorkflowStatistics> GetStatisticsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
