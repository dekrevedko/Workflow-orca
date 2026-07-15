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
                then => then
                    .Wait("Approved", state => new CorrelationId(state.CorrelationId))
                    .Then<TestStep>(),
                otherwise => otherwise.Then<TestStep>())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var ifNode = definition.RootSequence.Children.OfType<IfNode<TestState>>().Single();
        ifNode.Then.Children.Should().HaveCount(2);
        ifNode.Then.Children[0].Should().BeOfType<WaitNode<TestState>>();
        ifNode.Then.Children[1].Should().BeOfType<BusinessStepNode<TestState>>();
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
    public void BuildValidated_MultipleRootInitNodes_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("first"))
            .Init<string>(_ => new TestState("second"))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF017_MULTIPLE_INIT");
    }

    [Fact]
    public void BuildValidated_InitAfterExecutableNode_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Then<TestStep>()
            .Init<string>(_ => new TestState("late"))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Code == "WF018_INIT_NOT_FIRST");
    }

    [Fact]
    public void BuildValidated_NestedInitNode_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("root"))
            .If(_ => true, then => then.Init<string>(_ => new TestState("nested")))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF019_NESTED_INIT");
    }

    [Fact]
    public void BuildValidated_MultipleRootEndNodes_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .End("first")
            .End("second")
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF020_MULTIPLE_END");
    }

    [Fact]
    public void BuildValidated_NestedEndNode_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .If(_ => true, then => then.End("nested"))
            .End("root")
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF021_NESTED_END");
    }

    [Fact]
    public void BuildValidated_ExecutableNodeAfterRootEnd_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .End()
            .Then<TestStep>()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF022_NODE_AFTER_END");
    }

    [Fact]
    public void BuildValidated_BranchReturnAtWorkflowRoot_ReportsStableError()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .BranchReturn(state => state.CorrelationId)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Code == "WF026_BRANCH_RETURN_NOT_AT_SCOPE_EXIT");
    }

    [Fact]
    public void BuildValidated_RootContinueAsNewInsideConditional_IsValid()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .If(state => state.ShouldRoute, then => then.ContinueAsNew(state => state with { ShouldRoute = false }))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuildValidated_UnconditionalContinueAsNewBeforeEnd_ReportsUnreachableEnd()
    {
        var validation = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .ContinueAsNew(state => state)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == "WF028_UNREACHABLE_NODE");
    }

    [Fact]
    public void BuildValidated_MultipleProblems_ReportsAllAtOnce()
    {
        var validation = new WorkflowBuilder<TestState>()
            .If(null!, _ => { })
            .Delay(TimeSpan.Zero)
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            [
                BuilderValidationCodes.MissingInit,
                BuilderValidationCodes.NullDelegate,
                BuilderValidationCodes.NonPositiveDelay
            ]);
    }

    [Fact]
    public void Build_WithErrors_ThrowsAggregatedDefinitionException()
    {
        var builder = new WorkflowBuilder<TestState>()
            .If(null!, _ => { })
            .Delay(TimeSpan.Zero)
            .End();

        var act = () => builder.Build(DefinitionId.New(), DefinitionVersion.Initial);

        act.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain(BuilderValidationCodes.MissingInit)
            .And.Contain(BuilderValidationCodes.NullDelegate)
            .And.Contain(BuilderValidationCodes.NonPositiveDelay);
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
    public void End_WithOutcomeSelector_ResolvesFromFinalState()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .End(state => state.ShouldRoute ? "Approved" : "Rejected")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var end = definition.RootSequence.Children.OfType<EndNode<TestState>>().Single();

        end.ResolveOutcome(new TestState("accepted", ShouldRoute: true)).Should().Be("Approved");
        end.ResolveOutcome(new TestState("rejected", ShouldRoute: false)).Should().Be("Rejected");
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
    public void Build_WithoutDurableOnlyNodes_DoesNotRequireDurableEngine()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RequiresDurableEngine.Should().BeFalse();
    }

    [Fact]
    public void Build_WithTopLevelRunChild_RequiresDurableEngine()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .RunChild(DefinitionIdValue(2), DefinitionVersion.Initial)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RequiresDurableEngine.Should().BeTrue();
    }

    [Fact]
    public void Build_WithRunChildrenNestedInsideControlFlow_RequiresDurableEngine()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState("created"))
            .If(
                _ => true,
                then => then.While(
                    _ => false,
                    body => body.RunChildren(
                        DefinitionIdValue(2),
                        DefinitionVersion.Initial,
                        state => [state.CorrelationId])))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.RequiresDurableEngine.Should().BeTrue();
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
