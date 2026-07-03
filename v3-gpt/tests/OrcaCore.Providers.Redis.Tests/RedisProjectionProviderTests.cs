using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.Redis;
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

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
