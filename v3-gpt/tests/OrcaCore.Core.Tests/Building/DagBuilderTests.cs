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

    public static WorkflowDagBuilder Diamond()
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
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
