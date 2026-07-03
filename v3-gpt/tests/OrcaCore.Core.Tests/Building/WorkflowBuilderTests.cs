using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
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

    [Fact]
    public void Delay_WithPositiveDuration_AddsTimerNode()
    {
        var delay = TimeSpan.FromSeconds(30);

        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Delay(delay)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RootSequence.Children.OfType<DelayNode<TestState>>()
            .Single().Duration.Should().Be(delay);
    }

    [Fact]
    public void Delay_WithNonPositiveDuration_ReportsValidationError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Delay(TimeSpan.Zero)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.NonPositiveDelay);
    }

    [Fact]
    public void Build_DelayBeforeEnd_DoesNotCountAsEnd()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Delay(TimeSpan.FromSeconds(1))
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.MissingEnd);
    }

    [Fact]
    public void Then_GenericParameterlessStep_UsesTypedStepFactory()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var step = definition.RootSequence.Children
            .OfType<BusinessStepNode<TestState>>()
            .Single()
            .StepFactory();

        step.Should().BeOfType<TestStep>();
    }

    [Fact]
    public void Then_ConfiguredStepInstance_StoresExplicitFactory()
    {
        var configuredStep = new ConfiguredStep("x");

        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Then(configuredStep)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var step = definition.RootSequence.Children
            .OfType<BusinessStepNode<TestState>>()
            .Single()
            .StepFactory();

        step.Should().BeSameAs(configuredStep);
    }

    [Fact]
    public void RunChild_AddsDurableChildNode()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .RunChild(DefinitionIdValue(2), DefinitionVersion.Initial, RunChildFailurePolicy.ContinueParent)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var node = definition.RootSequence.Children.OfType<RunChildNode<TestState>>().Single();
        node.ChildDefinitionId.Should().Be(DefinitionIdValue(2));
        node.FailurePolicy.Should().Be(RunChildFailurePolicy.ContinueParent);
    }

    [Fact]
    public void RunChildren_AddsDurableFanoutNodeWithValidation()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .RunChildren(
                DefinitionIdValue(2),
                DefinitionVersion.Initial,
                state => [state.CorrelationId],
                maxConcurrency: 2,
                joinPolicy: RunChildrenJoinPolicy.WhenAny)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeTrue();
        var node = validation.Value.RootSequence.Children.OfType<RunChildrenNode<TestState>>().Single();
        node.ItemSnapshotSelector(new TestState("item")).Should().Equal("item");
        node.MaxConcurrency.Should().Be(2);
        node.JoinPolicy.Should().Be(RunChildrenJoinPolicy.WhenAny);
    }

    [Fact]
    public void RunChildren_WithNonPositiveMaxConcurrency_ReportsValidationError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .RunChildren(DefinitionIdValue(2), DefinitionVersion.Initial, _ => ["item"], maxConcurrency: 0)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.NonPositiveMaxConcurrency);
    }

    [Fact]
    [Trait("Scenario", "NEG-CP-001")]
    [Trait("AC", "CP-001")]
    public void NEG_CP_001_ParallelWithSingleBranch_BuildsDocumentedDegenerateBranch()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Parallel(("only", branch => branch.Then<TestStep>()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RootSequence.Children.OfType<ParallelNode<TestState>>()
            .Single()
            .Branches.Should().ContainSingle()
            .Which.BranchId.Name.Should().Be("only");
    }

    [Fact]
    [Trait("Scenario", "NEG-CP-002")]
    [Trait("AC", "CP-001")]
    public void NEG_CP_002_ParallelWithZeroBranches_ReportsValidationError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Parallel()
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.EmptyParallel);
    }

    [Fact]
    [Trait("Scenario", "NEG-CP-006")]
    [Trait("AC", "AC-601")]
    public void NEG_CP_006_ForEachNonPositiveMaxConcurrency_ReportsValidationError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .ForEach(
                _ => new[] { 1 },
                WorkflowPartitioner<int>.Items(),
                body => body.Then<TestStep>(),
                maxConcurrency: 0)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.NonPositiveMaxConcurrency);
    }

    [Fact]
    public void WorkflowBuilder_DoesNotExposeCompensationMethods()
    {
        var publicMethodNames = typeof(WorkflowBuilder<TestState>)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(WorkflowBuilder<TestState>))
            .Select(method => method.Name)
            .ToArray();

        publicMethodNames.Should().NotContain(
            [
                "CompensateBy",
                "CompensationScope",
                "Compensate"
            ]);
    }

    private sealed record TestState(string CorrelationId, bool ShouldRoute = true);

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ConfiguredStep(string name) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            _ = name;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
