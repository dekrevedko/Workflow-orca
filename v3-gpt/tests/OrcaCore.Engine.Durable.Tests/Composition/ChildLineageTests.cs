using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Composition;

public sealed class ChildLineageTests
{
    [Fact]
    public void ChildLineage_ProjectionCarriesParentAndRootIds()
    {
        var rootId = InstanceIdValue(1);
        var parentId = InstanceIdValue(2);
        var childId = InstanceIdValue(3);
        var command = StartCommand(childId) with
        {
            ParentInstanceId = parentId,
            RootInstanceId = rootId
        };
        var aggregate = DurableWorkflowAggregate.Empty(childId);

        var decision = aggregate.DecideStart(command);
        var started = decision.Events.Should().ContainSingle().Which.Should().BeOfType<WorkflowStartedEvent>().Subject;
        var projection = aggregate.CreateProjectionWrites(decision.Events).Should().ContainSingle().Subject;

        started.ParentInstanceId.Should().Be(parentId);
        started.RootInstanceId.Should().Be(rootId);
        projection.InstanceSnapshot!.ParentInstanceId.Should().Be(parentId);
        projection.InstanceSnapshot.RootInstanceId.Should().Be(rootId);
    }

    [Fact]
    [Trait("AC", "AC-614")]
    public async Task ChildLineage_QueryReturnsTree()
    {
        var rootId = InstanceIdValue(1);
        var childId = InstanceIdValue(2);
        var grandchildId = InstanceIdValue(3);
        var unrelatedId = InstanceIdValue(4);
        var store = new InMemoryWorkflowProvider();
        await SeedProjectionAsync(store, Snapshot(rootId, null, rootId));
        await SeedProjectionAsync(store, Snapshot(childId, rootId, rootId));
        await SeedProjectionAsync(store, Snapshot(grandchildId, childId, rootId));
        await SeedProjectionAsync(store, Snapshot(unrelatedId, null, unrelatedId));

        var tree = await new DurableManagement(store)
            .All()
            .Where(instance => instance.RootInstanceId == rootId)
            .ListAsync(TestContext.Current.CancellationToken);

        tree.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([rootId, childId, grandchildId]);
        tree.Should().ContainSingle(snapshot => snapshot.ParentInstanceId == childId);
    }

    private static async Task SeedProjectionAsync(
        InMemoryWorkflowProvider store,
        WorkflowInstanceSnapshot snapshot)
    {
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(snapshot.InstanceId),
                ExpectedVersion = StreamVersion.Empty,
                ProjectionOperations =
                [
                    new ProjectionWrite(snapshot.InstanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = snapshot
                    }
                ]
            },
            TestContext.Current.CancellationToken);
    }

    private static WorkflowInstanceSnapshot Snapshot(
        InstanceId instanceId,
        InstanceId? parentInstanceId,
        InstanceId rootInstanceId)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            ParentInstanceId = parentInstanceId,
            RootInstanceId = rootInstanceId,
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = WorkflowStatus.Running,
            CreatedAt = Timestamp(1),
            UpdatedAt = Timestamp(1)
        };
    }

    private static StartWorkflowCommand StartCommand(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
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

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
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
