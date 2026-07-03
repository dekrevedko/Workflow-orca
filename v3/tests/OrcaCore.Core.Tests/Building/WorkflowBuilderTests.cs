using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Tests.Building;

public class WorkflowBuilderTests
{
    private sealed class FakeState
    {
        public int Value { get; set; }
    }

    private sealed class NoopStep : IStep<FakeState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<FakeState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class MutatingStep(string tag) : IStep<FakeState>
    {
        public string Tag { get; } = tag;

        public ValueTask<StepResult> ExecuteAsync(StepContext<FakeState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public void Build_MinimalWorkflow_ProducesInitStepEndTree()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.Then<NoopStep>();
        builder.End();

        var definition = builder.Build(new DefinitionId("minimal"), new DefinitionVersion(1));

        definition.DefinitionId.Should().Be(new DefinitionId("minimal"));
        definition.DefinitionVersion.Should().Be(new DefinitionVersion(1));
        definition.Root.Steps.Should().HaveCount(3);
        definition.Root.Steps[0].Should().BeOfType<InitNode<FakeState, int>>();
        definition.Root.Steps[1].Should().BeOfType<BusinessStepNode<FakeState>>();
        definition.Root.Steps[2].Should().BeOfType<EndNode>();
    }

    [Fact]
    public void Build_NestedStructures_ProduceExpectedTree()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.If(
            state => ((FakeState)state!).Value > 0,
            then => then.Parallel(
                branch => branch.Wait("ApprovalReceived", state => new CorrelationId("order-1"))));
        builder.End();

        var definition = builder.Build(new DefinitionId("nested"), new DefinitionVersion(1));

        definition.Root.Steps.Should().HaveCount(3);
        definition.Root.Steps[1].Should().BeOfType<IfNode>();
        var ifNode = (IfNode)definition.Root.Steps[1];
        ifNode.Then.Steps.Should().ContainSingle().Which.Should().BeOfType<ParallelNode>();
        var parallelNode = (ParallelNode)ifNode.Then.Steps[0];
        parallelNode.Branches.Should().ContainSingle();
        parallelNode.Branches[0].Body.Steps.Should().ContainSingle().Which.Should().BeOfType<WaitNode>();
    }

    [Fact]
    public void BuildValidated_MissingInit_ReportsError()
    {
        var builder = WorkflowBuilder<FakeState>.CreateWithoutInit();
        builder.End();

        var result = builder.BuildValidated(new DefinitionId("no-init"), new DefinitionVersion(1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == WorkflowBuilderValidationCodes.MissingInit);
    }

    [Fact]
    public void BuildValidated_MultipleProblems_ReportsAllAtOnce()
    {
        var builder = WorkflowBuilder<FakeState>.CreateWithoutInit();
        builder.If(condition: null!, then => then.End());
        builder.Parallel();
        builder.End();

        var result = builder.BuildValidated(new DefinitionId("multi-problem"), new DefinitionVersion(1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Select(e => e.Code).Should().BeEquivalentTo(
        [
            WorkflowBuilderValidationCodes.MissingInit,
            WorkflowBuilderValidationCodes.NullCondition,
            WorkflowBuilderValidationCodes.EmptyParallel,
        ]);
    }

    [Fact]
    public void Build_WithErrors_ThrowsAggregatedDefinitionException()
    {
        var builder = WorkflowBuilder<FakeState>.CreateWithoutInit();
        builder.Parallel();
        builder.End();

        var act = () => builder.Build(new DefinitionId("throws"), new DefinitionVersion(1));

        var exception = act.Should().Throw<WorkflowDefinitionException>().Which;
        exception.Message.Should().Contain(WorkflowBuilderValidationCodes.MissingInit);
        exception.Message.Should().Contain(WorkflowBuilderValidationCodes.EmptyParallel);
    }

    [Fact]
    public void Build_Twice_ProducesEqualIndependentDefinitions()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.Then<NoopStep>();
        builder.End();

        var first = builder.Build(new DefinitionId("twice"), new DefinitionVersion(1));
        var second = builder.Build(new DefinitionId("twice"), new DefinitionVersion(1));

        first.Should().BeEquivalentTo(second);
        first.Root.Should().NotBeSameAs(second.Root);
    }

    [Fact]
    public void End_WithOutcomeName_LandsOnEndNode()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.End("Approved");

        var definition = builder.Build(new DefinitionId("outcome"), new DefinitionVersion(1));

        var endNode = (EndNode)definition.Root.Steps[^1];
        endNode.OutcomeName.Should().Be("Approved");
    }

    [Fact]
    public void Then_GenericParameterlessStep_UsesTypedStepFactory()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.Then<NoopStep>();
        builder.End();

        var definition = builder.Build(new DefinitionId("typed-step"), new DefinitionVersion(1));

        var stepNode = (BusinessStepNode<FakeState>)definition.Root.Steps[1];
        stepNode.CreateStep().Should().BeOfType<NoopStep>();
    }

    [Fact]
    public void Then_ConfiguredStepInstance_StoresExplicitFactory()
    {
        var builder = WorkflowBuilder<FakeState>.Create<int>(input => new FakeState { Value = input });
        builder.Then(new MutatingStep("x"));
        builder.End();

        var definition = builder.Build(new DefinitionId("configured-step"), new DefinitionVersion(1));

        var stepNode = (BusinessStepNode<FakeState>)definition.Root.Steps[1];
        var step = stepNode.CreateStep();
        step.Should().BeOfType<MutatingStep>();
        ((MutatingStep)step).Tag.Should().Be("x");
    }
}
