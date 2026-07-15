using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class BranchContractTests
{
    [Fact]
    public void BranchesMayUseDifferentPrivateStates_ButReturnOneDeclaredResultRecord()
    {
        var definition = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new ParentState("order"))
            .Parallel<BranchOutcome>(
                branches => branches
                    .Branch<TextBranchState>("text", parent => new TextBranchState(parent.Value.Value), branch =>
                        branch.Return(state => new BranchOutcome("text", state.Value.Value.Length)))
                    .Branch<NumberBranchState>("number", _ => new NumberBranchState(42), branch =>
                        branch.Return(state => new BranchOutcome("number", state.Value.Value))),
                (parent, _) => parent.Value)
            .End()
            .Build();

        var scope = definition.CompiledPlan.Scopes.Should().ContainSingle().Which;
        scope.Branches.Select(branch => branch.Input.BranchStateType)
            .Should().Equal(typeof(TextBranchState), typeof(NumberBranchState));
        scope.Branches.Select(branch => branch.Result.ResultType)
            .Should().OnlyContain(type => type == typeof(BranchOutcome));
    }

    [Fact]
    public void ForEachOutcomeCarriesStableItemIndexAndTerminalMetadata()
    {
        var outcomes = new[]
        {
            new ForEachItemOutcome<string>(1, ForEachItemTerminalStatus.Failed, null, "failed"),
            new ForEachItemOutcome<string>(0, ForEachItemTerminalStatus.Succeeded, "zero", null)
        };

        outcomes.OrderBy(outcome => outcome.Index).Select(outcome => outcome.Index)
            .Should().Equal(0, 1);
        outcomes[0].Status.Should().Be(ForEachItemTerminalStatus.Failed);
        typeof(StepResult).GetNestedTypes().Select(type => type.Name)
            .Should().NotContain(name => name.Contains("BusinessResult", StringComparison.Ordinal));
    }

    [Fact]
    public void BranchMayContainNestedScopeThatMergesIntoPrivateStateBeforeOuterReturn()
    {
        var definition = Workflow.Durable<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value))
            .Parallel<string>(
                branches => branches
                    .Branch<OuterBranchState>(
                        "nested",
                        _ => new OuterBranchState("nested", 0),
                        branch => branch
                            .Parallel<int>(
                                nested => nested
                                    .Branch<NumberBranchState>(
                                        "one",
                                        _ => new NumberBranchState(1),
                                        child => child.Return(state => state.Value.Value))
                                    .Branch<NumberBranchState>(
                                        "two",
                                        _ => new NumberBranchState(2),
                                        child => child.Return(state => state.Value.Value)),
                                (parent, results) => parent.Value with
                                {
                                    Total = results.Sum(result => result.Value)
                                })
                            .Return(state => $"{state.Value.Name}:{state.Value.Total}"))
                    .Branch<TextBranchState>(
                        "plain",
                        _ => new TextBranchState("plain"),
                        branch => branch.Return(state => state.Value.Value)),
                (parent, _) => parent.Value)
            .End()
            .Build();

        definition.CompiledPlan.Scopes.Should().HaveCount(2);
        definition.CompiledPlan.Scopes.Should().Contain(scope =>
            scope.Merge.ParentStateType == typeof(OuterBranchState));
        definition.CompiledPlan.Instructions.Count(instruction =>
            instruction.Kind == Core.Compilation.CompiledInstructionKind.StartScope).Should().Be(2);
    }

    private sealed record ParentState(string Value);

    private sealed record TextBranchState(string Value);

    private sealed record NumberBranchState(int Value);

    private sealed record OuterBranchState(string Name, int Total);

    private sealed record BranchOutcome(string Kind, int Value);
}
