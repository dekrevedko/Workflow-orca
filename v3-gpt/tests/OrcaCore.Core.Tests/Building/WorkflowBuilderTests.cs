using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class WorkflowBuilderTests
{
    [Fact]
    public void Build_MinimalWorkflow_ProducesInitStepEndTree()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RootSequence.Children.Should().HaveCount(3);
        definition.RootSequence.Children[0].Should().BeOfType<InitNode<TestState>>();
        definition.RootSequence.Children[1].Should().BeOfType<BusinessStepNode<TestState>>();
        definition.RootSequence.Children[2].Should().BeOfType<EndNode<TestState>>();
    }

    [Fact]
    public void Build_NestedStructures_ProduceExpectedTree()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .If(
                state => state.ShouldRoute,
                then => then.Parallel(("approval", branch => branch.Wait(
                    "Approved",
                    state => new CorrelationId(state.CorrelationId)).End("Approved"))),
                otherwise => otherwise.End("Skipped"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var ifNode = definition.RootSequence.Children.OfType<IfNode<TestState>>().Single();
        var parallel = ifNode.Then.Children.Should().ContainSingle().Which.Should().BeOfType<ParallelNode<TestState>>().Subject;

        parallel.Branches.Should().ContainSingle().Which.BranchId.Name.Should().Be("approval");
        parallel.Branches.Single().Sequence.Children[0].Should().BeOfType<WaitNode<TestState>>();
    }

    [Fact]
    public void BuildValidated_MissingInit_ReportsError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.MissingInit);
    }

    [Fact]
    public void BuildValidated_MultipleProblems_ReportsAllAtOnce()
    {
        var validation = new WorkflowBuilder<TestState>()
            .If(null!, then => then.Parallel(("empty", _ => { })))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            [
                BuilderValidationCodes.MissingInit,
                BuilderValidationCodes.NullDelegate,
                BuilderValidationCodes.EmptyBranch
            ]);
    }

    [Fact]
    public void Build_WithErrors_ThrowsAggregatedDefinitionException()
    {
        var builder = new WorkflowBuilder<TestState>()
            .If(null!, then => then.Parallel(("empty", _ => { })))
            .End();

        var act = () => builder.Build(DefinitionId.New(), DefinitionVersion.Initial);

        act.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain(BuilderValidationCodes.MissingInit)
            .And.Contain(BuilderValidationCodes.NullDelegate)
            .And.Contain(BuilderValidationCodes.EmptyBranch);
    }

    [Fact]
    public void Build_Twice_ProducesEqualIndependentDefinitions()
    {
        var definitionId = DefinitionId.New();
        var builder = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Then(() => new TestStep())
            .End();

        var first = builder.Build(definitionId, DefinitionVersion.Initial);
        var second = builder.Build(definitionId, DefinitionVersion.Initial);

        first.Should().NotBeSameAs(second);
        first.DefinitionId.Should().Be(second.DefinitionId);
        first.DefinitionVersion.Should().Be(second.DefinitionVersion);
        first.RootSequence.Children.Select(node => node.GetType()).Should()
            .Equal(second.RootSequence.Children.Select(node => node.GetType()));
    }

    [Fact]
    public void End_WithOutcomeName_LandsOnEndNode()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .End("Approved")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RootSequence.Children.OfType<EndNode<TestState>>()
            .Single().OutcomeName.Should().Be("Approved");
    }

    private sealed record TestState(string CorrelationId, bool ShouldRoute = true);

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
