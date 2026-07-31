using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class DagBuilderTests
{
    [Fact]
    [Trait("AC", "JS-AC-002")]
    public void Build_WhenDagHasCycle_ReturnsAccumulatedCycleDiagnostic()
    {
        var validation = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .DependsOn("A", "B")
            .DependsOn("B", "A")
            .BuildValidated();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error =>
            error.Code == BuilderValidationCodes.DagCycle &&
            error.Message.Contains("A", StringComparison.Ordinal) &&
            error.Message.Contains("B", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WhenDagHasDiamondShape_ProducesDependencyJoinPlan()
    {
        var plan = Diamond().BuildValidated().Value;

        plan.Node("D").Dependencies.Should().BeEquivalentTo(["B", "C"]);
        plan.GetRunnableNodes(["A", "B"], []).Select(node => node.NodeId).Should().Equal("C");
        plan.GetRunnableNodes(["A", "B", "C"], []).Select(node => node.NodeId).Should().Equal("D");
    }

    [Fact]
    public void CreateChildBatches_WhenRunnableNodesUseDifferentDefinitions_GroupsByDefinition()
    {
        var plan = Diamond().BuildValidated().Value;
        var runnable = plan.GetRunnableNodes(["A"], []);

        var batches = plan.CreateChildBatches(runnable);

        batches.Should().HaveCount(2);
        batches.Select(batch => batch.ChildDefinitionId).Should().BeEquivalentTo(
            [DefinitionIdValue(2), DefinitionIdValue(3)]);
        batches.SelectMany(batch => batch.ItemSnapshots).Should().BeEquivalentTo("B", "C");
        var act = () => plan.CreateChildBatch(runnable);
        act.Should().Throw<ArgumentException>().WithMessage("*heterogeneous*");
    }

    [Fact]
    public void CreateChildBatch_WhenThrottleCapIsProvided_ClampsMaxConcurrency()
    {
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(1), DefinitionVersion.Initial)
            .BuildValidated()
            .Value;

        var batch = plan.CreateChildBatch(plan.GetRunnableNodes([], []), maxConcurrency: 1);

        batch.ItemSnapshots.Should().Equal("A", "B");
        batch.MaxConcurrency.Should().Be(1);
    }

    [Fact]
    public void WorkflowDagRunner_ReturnsNextBatchesFromCompletionState()
    {
        var plan = Diamond().BuildValidated().Value;
        var runner = new WorkflowDagRunner(plan, maxConcurrency: 1);

        var batches = runner.GetNextBatches(["A"], []);

        batches.Should().HaveCount(2);
        batches.Should().OnlyContain(batch => batch.MaxConcurrency == 1);
        batches.SelectMany(batch => batch.ItemSnapshots).Should().BeEquivalentTo("B", "C");
    }

    [Fact]
    public void GetBlockedByFailures_ReportsTransitivelyBlockedNodes()
    {
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("C", DefinitionIdValue(3), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "B")
            .BuildValidated()
            .Value;

        plan.GetBlockedByFailures(["A"]).Select(node => node.NodeId).Should().Equal("B", "C");
    }

    [Fact]
    public void GetRunnableNodes_ExcludesScheduledButNotYetTerminalNodes()
    {
        var plan = Diamond().BuildValidated().Value;

        // A completed, B and C already scheduled (in-flight): neither B, C, nor D is runnable.
        plan.GetRunnableNodes(["A"], [], scheduledNodeIds: ["B", "C"]).Should().BeEmpty();

        // Without the scheduled set, B and C would be re-returned — the double-scheduling hazard.
        plan.GetRunnableNodes(["A"], []).Select(node => node.NodeId).Should().Equal("B", "C");
    }

    [Fact]
    public void IsComplete_WhenRemainingNodesAreTransitivelyBlocked_ReportsTerminalRun()
    {
        var plan = new WorkflowDagBuilder()
            .Node("A", DefinitionIdValue(1), DefinitionVersion.Initial)
            .Node("B", DefinitionIdValue(2), DefinitionVersion.Initial)
            .Node("C", DefinitionIdValue(3), DefinitionVersion.Initial)
            .Node("D", DefinitionIdValue(4), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "B")
            .BuildValidated()
            .Value;

        plan.IsComplete(["D"], ["A"]).Should().BeTrue();
        plan.IsComplete(["D"], []).Should().BeFalse();

        var runner = new WorkflowDagRunner(plan);
        runner.GetNextBatches(["D"], ["A"]).Should().BeEmpty();
        runner.IsComplete(["D"], ["A"]).Should().BeTrue();
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

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
