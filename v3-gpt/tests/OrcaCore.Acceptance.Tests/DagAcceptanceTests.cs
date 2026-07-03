using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class DagAcceptanceTests
{
    [Fact]
    [Trait("AC", "JS-AC-001")]
    public async Task DiamondDag_StartsJoinNodeAfterBothParentsCompleteInAnyOrder()
    {
        var plan = Diamond().BuildValidated().Value;
        var parentId = InstanceIdValue(10);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(Start(parentId), TestContext.Current.CancellationToken);

        await ProcessBatchesAsync(processor, parentId, 2, Timestamp(2), plan.CreateChildBatches(plan.GetRunnableNodes([], [])));
        await ProcessBatchesAsync(processor, parentId, 3, Timestamp(3), plan.CreateChildBatches(plan.GetRunnableNodes(["A"], [])));
        plan.GetRunnableNodes(["A", "C"], []).Select(node => node.NodeId).Should().NotContain("D");
        await ProcessBatchesAsync(processor, parentId, 5, Timestamp(4), plan.CreateChildBatches(plan.GetRunnableNodes(["A", "C", "B"], [])));

        var scheduled = (await store.LoadTailAsync(new WorkflowStreamId(parentId), StreamVersion.Empty, TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Select(child => child.ItemSnapshot))
            .ToArray();

        scheduled.Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    [Trait("AC", "JS-AC-003")]
    public void FailingDagNode_PreventsDependentNodesAndAppliesFailurePolicy()
    {
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("C", DefinitionIdValue(3), DefinitionVersion.Initial)
            .Node("D", DefinitionIdValue(4), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .DependsOn("D", "B")
            .BuildValidated()
            .Value;

        plan.GetRunnableNodes(["A"], ["B"]).Select(node => node.NodeId).Should().Equal("C");
        plan.GetBlockedByFailures(["B"]).Select(node => node.NodeId).Should().Equal("D");
    }

    private static WorkflowDagBuilder Diamond()
    {
        return new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("C", DefinitionIdValue(3), DefinitionVersion.Initial)
            .Node("D", DefinitionIdValue(4), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .DependsOn("D", "B")
            .DependsOn("D", "C");
    }

    private static StartWorkflowCommand Start(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(100),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DurableRunChildrenCommand ToCommand(
        InstanceId parentInstanceId,
        CommandId commandId,
        DateTimeOffset requestedAt,
        WorkflowDagChildBatch batch)
    {
        return new DurableRunChildrenCommand(
            commandId,
            parentInstanceId,
            requestedAt,
            batch.ChildDefinitionId,
            batch.ChildDefinitionVersion,
            batch.ItemSnapshots,
            batch.FailurePolicy,
            batch.MaxConcurrency);
    }

    private static async Task ProcessBatchesAsync(
        DurableCommandProcessor processor,
        InstanceId parentInstanceId,
        int commandStart,
        DateTimeOffset requestedAt,
        IReadOnlyList<WorkflowDagChildBatch> batches)
    {
        for (var index = 0; index < batches.Count; index++)
        {
            await processor.ProcessAsync(
                ToCommand(parentInstanceId, CommandIdValue(commandStart + index), requestedAt, batches[index]),
                TestContext.Current.CancellationToken);
        }
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 21, minutes, 0, TimeSpan.Zero);
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
