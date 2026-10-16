using AwesomeAssertions;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.ResourceGovernance;

public sealed class DurableResourcePoolManagementTests
{
    [Fact]
    public async Task StartupRetainsReplayedCurrentCapacity_AndResizeIsIdempotentByOperation()
    {
        var store = new InMemoryResourceGovernanceStore();
        var partition = ResourceGovernancePartitionId.Create("default");
        var options = Options(partition, capacity: 3);
        var first = new DurableResourcePoolManagement(store, options);
        await first.InitializeAsync(TestContext.Current.CancellationToken);

        var operation = ResourcePoolOperationId.Create("resize-1");
        var applied = await first.ResizeAsync(
            ResourcePoolName.Create("db"),
            1,
            operation,
            TestContext.Current.CancellationToken);
        var replay = await first.ResizeAsync(
            ResourcePoolName.Create("db"),
            1,
            operation,
            TestContext.Current.CancellationToken);

        var replacement = new DurableResourcePoolManagement(store, options);
        await replacement.InitializeAsync(TestContext.Current.CancellationToken);
        var snapshot = await replacement.GetAsync(
            ResourcePoolName.Create("db"),
            TestContext.Current.CancellationToken);

        applied.Should().BeOfType<DurableResourcePoolResizeResult.Applied>();
        replay.Should().Be(applied);
        snapshot.ConfiguredCapacity.Should().Be(1);

        var incompatible = new DurableResourcePoolManagement(store, Options(partition, capacity: 1));
        await incompatible.Invoking(candidate =>
                candidate.InitializeAsync(TestContext.Current.CancellationToken).AsTask())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ResizeRejectsInvalidInputBeforeBinding_AndReportsChangedIntentConflict()
    {
        var store = new InMemoryResourceGovernanceStore();
        var management = new DurableResourcePoolManagement(
            store,
            Options(ResourceGovernancePartitionId.Create("default"), capacity: 2));
        await management.InitializeAsync(TestContext.Current.CancellationToken);
        var operation = ResourcePoolOperationId.Create("resize-1");

        await management.Invoking(candidate => candidate.ResizeAsync(
                ResourcePoolName.Create("db"),
                0,
                operation,
                TestContext.Current.CancellationToken).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await management.Invoking(candidate => candidate.ResizeAsync(
                ResourcePoolName.Create("missing"),
                1,
                operation,
                TestContext.Current.CancellationToken).AsTask())
            .Should().ThrowAsync<ResourcePoolNotConfiguredException>();

        await management.ResizeAsync(
            ResourcePoolName.Create("db"),
            1,
            operation,
            TestContext.Current.CancellationToken);
        var conflict = await management.ResizeAsync(
            ResourcePoolName.Create("db"),
            2,
            operation,
            TestContext.Current.CancellationToken);

        conflict.Should().BeEquivalentTo(
            new DurableResourcePoolResizeResult.Conflict(
                operation,
                ResourcePoolName.Create("db"),
                1,
                ResourcePoolName.Create("db"),
                2));
        (await management.GetAsync(
            ResourcePoolName.Create("db"),
            TestContext.Current.CancellationToken)).ConfiguredCapacity.Should().Be(1);
    }

    private static DurableResourcePoolOptions Options(
        ResourceGovernancePartitionId partition,
        int capacity) =>
        new()
        {
            PartitionId = partition,
            Pools =
            [
                DurableResourcePoolDefinition.Create(
                    ResourcePoolName.Create("db"),
                    capacity,
                    TimeSpan.FromMinutes(5))
            ]
        };
}
