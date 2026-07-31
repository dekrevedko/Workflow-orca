using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class BranchContractTests
{
    [Fact]
    public void PublicOutcomeFamilies_AreClosedSuccessFailureOnlyAndDetachCauses()
    {
        var branchVariants = typeof(global::OrcaCore.BranchOutcome<string>).GetNestedTypes();
        var itemVariants = typeof(global::OrcaCore.ForEachItemOutcome<string>).GetNestedTypes();

        branchVariants.Select(type => type.Name).Should().BeEquivalentTo("Succeeded", "Failed");
        itemVariants.Select(type => type.Name).Should().BeEquivalentTo("Succeeded", "Failed");
        branchVariants.Concat(itemVariants).Should().OnlyContain(type => type.IsSealed);
        branchVariants.Concat(itemVariants)
            .SelectMany(type => type.GetConstructors())
            .Should().BeEmpty("runtime-created outcome variants must not expose public constructors");
        typeof(global::OrcaCore.WorkflowFailure).GetProperty("Causes")!.PropertyType
            .Should().Be<IReadOnlyList<global::OrcaCore.WorkflowFailure>>();
        typeof(global::OrcaCore.WorkflowFailure).GetConstructors()
            .Should().BeEmpty("workflow failures are detached runtime values, not consumer-authored values");
    }

    [Fact]
    public void ForEachOptions_FactoryRejectsInvalidBoundsEagerly()
    {
        var valid = global::OrcaCore.ForEachOptions.Create(3, 2);
        valid.MaxItems.Should().Be(3);
        valid.MaxConcurrency.Should().Be(2);

        Action noItems = () => global::OrcaCore.ForEachOptions.Create(0);
        Action noConcurrency = () => global::OrcaCore.ForEachOptions.Create(1, 0);

        noItems.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxItems");
        noConcurrency.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maxConcurrency");
    }

    [Fact]
    public void BranchesMayUseDifferentPrivateStates_ButReturnOneDeclaredResultRecord()
    {
        var definition = global::OrcaCore.Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ParentState("order"))
            .Parallel<BranchOutcome>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create("text"),
                        parent => new TextBranchState(parent.Value.Value),
                        branch =>
                        branch.Return(state => new BranchOutcome("text", state.Value.Value.Length)))
                    .Branch(
                        AuthoredBranchId.Create("number"),
                        _ => new NumberBranchState(42),
                        branch => branch.Return(state => new BranchOutcome("number", state.Value.Value))))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

        definition.Mode.Should().Be(WorkflowMode.Ephemeral);
        definition.DefinitionFingerprint.Should().NotBeNull(
            "heterogeneous private branch states compile behind one declared public result type");
    }

    [Fact]
    public void ForEachOutcomeCarriesStableItemIndexAndTerminalMetadata()
    {
        var outcome = typeof(global::OrcaCore.ForEachItemOutcome<string>);
        outcome.GetProperty("Index")!.PropertyType.Should().Be<int>();
        typeof(global::OrcaCore.ForEachItemOutcome<string>.Succeeded)
            .GetProperty("Result")!.PropertyType.Should().Be<string>();
        typeof(global::OrcaCore.ForEachItemOutcome<string>.Failed)
            .GetProperty("Failure")!.PropertyType.Should().Be<global::OrcaCore.WorkflowFailure>();
        typeof(StepResult).GetNestedTypes().Select(type => type.Name)
            .Should().NotContain(name => name.Contains("BusinessResult", StringComparison.Ordinal));
    }

    private sealed record ParentState(string Value);

    private sealed record TextBranchState(string Value);

    private sealed record NumberBranchState(int Value);

    private sealed record BranchOutcome(string Kind, int Value);
}
