using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class DagObservabilityAcceptanceTests
{
    [Fact]
    [Trait("AC", "JS-AC-005")]
    public async Task ReconstructDagRun_AfterNodeCompletionAndFailure_ReturnsNodeStatusesAndTimings()
    {
        var rootId = InstanceIdValue(10);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var plan = new WorkflowDagBuilder()
            .Node("extract", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("load", DefinitionIdValue(2), DefinitionVersion.Initial)
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(Start(rootId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(ToCommand(rootId, plan.CreateChildBatch(plan.GetRunnableNodes([], []))), TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .Single();
        var extract = scheduled.Children.Single(child => child.ItemSnapshot == "extract").ChildInstanceId;
        var load = scheduled.Children.Single(child => child.ItemSnapshot == "load").ChildInstanceId;
        await SeedProjectionAsync(store, Snapshot(extract, rootId, rootId, WorkflowStatus.Completed, Timestamp(11), Timestamp(15)));
        await SeedProjectionAsync(store, Snapshot(load, rootId, rootId, WorkflowStatus.Failed, Timestamp(12), Timestamp(16), "load failed"));

        var snapshot = await new DurableManagement(store)
            .ReconstructDagRunAsync(rootId, TestContext.Current.CancellationToken);

        snapshot.RootInstanceId.Should().Be(rootId);
        snapshot.Nodes.Select(node => node.NodeId).Should().Equal("extract", "load");
        snapshot.Nodes.Single(node => node.NodeId == "extract").Status.Should().Be(WorkflowStatus.Completed);
        snapshot.Nodes.Single(node => node.NodeId == "extract").StartedAt.Should().Be(Timestamp(11));
        snapshot.Nodes.Single(node => node.NodeId == "extract").UpdatedAt.Should().Be(Timestamp(15));
        snapshot.Nodes.Single(node => node.NodeId == "load").Status.Should().Be(WorkflowStatus.Failed);
        snapshot.Nodes.Single(node => node.NodeId == "load").ErrorSummary.Should().Be("load failed");
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

    private static StartWorkflowCommand Start(InstanceId instanceId)
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

    private static DurableRunChildrenCommand ToCommand(InstanceId rootId, WorkflowDagChildBatch batch)
    {
        return new DurableRunChildrenCommand(
            CommandIdValue(2),
            rootId,
            Timestamp(2),
            batch.ChildDefinitionId,
            batch.ChildDefinitionVersion,
            batch.ItemSnapshots,
            batch.FailurePolicy,
            batch.MaxConcurrency);
    }

    private static WorkflowInstanceSnapshot Snapshot(
        InstanceId instanceId,
        InstanceId parentInstanceId,
        InstanceId rootInstanceId,
        WorkflowStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? errorSummary = null)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            ParentInstanceId = parentInstanceId,
            RootInstanceId = rootInstanceId,
            DefinitionId = DefinitionIdValue(2),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ErrorSummary = errorSummary
        };
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 23, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
