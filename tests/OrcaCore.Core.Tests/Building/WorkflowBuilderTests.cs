using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

/// <summary>
/// Ports the supported consumer-facing coverage from the retired catch-all builder suite.
/// Invalid graph shapes that the staged API now makes unrepresentable are asserted from
/// the public surface instead of being constructed through the old permissive builder.
/// </summary>
public sealed class WorkflowBuilderTests
{
    [Fact]
    public void Build_MinimalWorkflow_ProducesStablePublicDefinitionMetadata()
    {
        var definitionId = DefinitionId.New();

        var definition = Workflow.Ephemeral<TestState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build();

        definition.Mode.Should().Be(WorkflowMode.Ephemeral);
        definition.DefinitionId.Should().Be(definitionId);
        definition.DefinitionVersion.Should().Be(DefinitionVersion.Initial);
        definition.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Build_NestedStructures_ProduceDifferentStructuralFingerprint()
    {
        var definitionId = DefinitionId.New();
        var minimal = BuildMinimal(definitionId);

        var nested = Workflow.Ephemeral<TestState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .If(
                state => state.Value.ShouldRoute,
                then => then
                    .Wait(
                        EventName.Create("approved"),
                        state => CorrelationId.Create(state.Value.CorrelationId))
                    .Then<TestStep>(),
                otherwise => otherwise.Then<TestStep>())
            .End()
            .Build();

        nested.DefinitionFingerprint.Should().NotBe(minimal.DefinitionFingerprint);
    }

    [Fact]
    public void MissingInit_IsCompileImpossibleThroughTheInitStage()
    {
        DeclaredMethods(typeof(EphemeralWorkflowInitBuilder<TestState>))
            .Should().Equal("Init");
        DeclaredMethods(typeof(DurableWorkflowInitBuilder<TestState>))
            .Should().Equal("Init");
    }

    [Fact]
    public void MultipleRootInitNodes_AreCompileImpossibleAfterInit()
    {
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().NotContain("Init");
        DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>))
            .Should().NotContain("Init");
    }

    [Fact]
    public void InitAfterExecutableNode_IsCompileImpossible()
    {
        var builderType = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .GetType();

        DeclaredMethods(builderType).Should().NotContain("Init");
    }

    [Fact]
    public void NestedInit_IsCompileImpossible()
    {
        DeclaredMethods(typeof(EphemeralNestedBuilder<string, TestState>))
            .Should().NotContain("Init");
        DeclaredMethods(typeof(DurableNestedBuilder<string, TestState>))
            .Should().NotContain("Init");
    }

    [Fact]
    public void MultipleRootEnds_AreCompileImpossibleAfterCompletionSelection()
    {
        AssertCompletionStage(typeof(EphemeralWorkflowCompletionBuilder<string>));
        AssertCompletionStage(typeof(DurableWorkflowCompletionBuilder<string>));
    }

    [Fact]
    public void NestedEnd_IsCompileImpossible()
    {
        DeclaredMethods(typeof(EphemeralNestedBuilder<string, TestState>))
            .Should().NotContain("End");
        DeclaredMethods(typeof(DurableNestedBuilder<string, TestState>))
            .Should().NotContain("End");
    }

    [Fact]
    public void ExecutableNodeAfterRootEnd_IsCompileImpossible()
    {
        var completionType = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .End()
            .GetType();

        DeclaredMethods(completionType).Should().Equal("Build", "TryBuild");
    }

    [Fact]
    public void BranchReturnAtWorkflowRoot_IsCompileImpossible()
    {
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().NotContain("Return");
        DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>))
            .Should().NotContain("Return");
    }

    [Fact]
    public void ContinueAsNew_IsDurableRootOnly()
    {
        DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>))
            .Should().Contain("ContinueAsNew");
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().NotContain("ContinueAsNew");
        DeclaredMethods(typeof(DurableNestedBuilder<string, TestState>))
            .Should().NotContain("ContinueAsNew");
    }

    [Fact]
    public void ContinueAsNew_IsTerminalAndCannotBeFollowedByEnd()
    {
        var completion = Workflow.Durable<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .ContinueAsNew(state => state.Value);

        completion.Build().Mode.Should().Be(WorkflowMode.Durable);
        DeclaredMethods(completion.GetType()).Should().Equal("Build", "TryBuild");
    }

    [Fact]
    public void InvalidFluentArguments_AreRejectedAtTheCallSite()
    {
        var builder = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"));

        Action nullCondition = () => builder.If(null!, _ => { });
        Action zeroDelay = () => builder.Delay(TimeSpan.Zero);

        nullCondition.Should().Throw<ArgumentNullException>();
        zeroDelay.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_WithGraphErrors_ThrowsAggregatedDefinitionException()
    {
        var completion = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Parallel<int>(_ => { })
            .WhenAll((state, _) => state.Value)
            .End();

        Action build = () => completion.Build();

        build.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle(
                diagnostic => diagnostic.Code == "SFE-AUTH-BRANCH-004");
    }

    [Fact]
    public void Build_Twice_ProducesEquivalentIndependentDefinitions()
    {
        var completion = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End();

        var first = completion.Build();
        var second = completion.Build();

        first.Should().NotBeSameAs(second);
        first.DefinitionId.Should().Be(second.DefinitionId);
        first.DefinitionVersion.Should().Be(second.DefinitionVersion);
        first.DefinitionFingerprint.Should().Be(second.DefinitionFingerprint);
    }

    [Fact]
    public void End_WithFixedOutcome_ChangesTheStructuralFingerprint()
    {
        var definitionId = DefinitionId.New();
        var baseline = Workflow.Ephemeral<TestState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .End()
            .Build();
        var named = Workflow.Ephemeral<TestState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .End(WorkflowOutcomeName.Create("approved"))
            .Build();

        named.DefinitionFingerprint.Should().NotBe(baseline.DefinitionFingerprint);
    }

    [Fact]
    public void DynamicOutcomeSelector_IsNotExposed()
    {
        var outcomeOverloads = typeof(EphemeralWorkflowBuilder<string, TestState>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "End")
            .Where(method => !method.IsGenericMethodDefinition)
            .ToArray();

        outcomeOverloads.Should().HaveCount(2);
        outcomeOverloads.Should().ContainSingle(method => method.GetParameters().Length == 0);
        outcomeOverloads.Should().ContainSingle(method =>
            method.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(new[] { typeof(WorkflowOutcomeName) }));
    }

    [Fact]
    public void Delay_WithPositiveDuration_ChangesTheStructuralFingerprint()
    {
        var definitionId = DefinitionId.New();

        BuildWithDelay(definitionId, TimeSpan.FromSeconds(30)).DefinitionFingerprint
            .Should().NotBe(BuildWithDelay(
                definitionId,
                TimeSpan.FromSeconds(31)).DefinitionFingerprint);
    }

    [Fact]
    public void Delay_WithNonPositiveDuration_IsRejectedEagerly()
    {
        var builder = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"));

        Action zero = () => builder.Delay(TimeSpan.Zero);
        Action negative = () => builder.Delay(TimeSpan.FromTicks(-1));

        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MissingEnd_IsCompileImpossibleFromTheRootBuilder()
    {
        var methods = DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>));

        methods.Should().NotContain("Build");
        methods.Should().NotContain("TryBuild");
        methods.Should().Contain("End");
    }

    [Fact]
    public void Then_GenericNamedStep_BuildsThroughThePublicSurface()
    {
        var definition = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build();

        definition.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Then_InlineStep_BuildsWithoutAnImplicitConfiguredStepInstance()
    {
        var definition = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then((StepContext<TestState> _) => ValueTask.CompletedTask)
            .End()
            .Build();

        definition.DefinitionFingerprint.Value.Should().NotBeNullOrWhiteSpace();
        typeof(EphemeralWorkflowBuilder<string, TestState>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == "Then")
            .Should().NotContain(method =>
                method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(IStep<TestState>)));
    }

    [Fact]
    public void EphemeralDefinition_UsesTheExplicitEphemeralMode()
    {
        BuildMinimal(DefinitionId.New()).Mode.Should().Be(WorkflowMode.Ephemeral);
    }

    [Fact]
    public void WorkflowBuilders_DoNotExposeCompensationMethods()
    {
        var publicMethodNames = DeclaredMethods(
                typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Concat(DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>)))
            .ToArray();

        publicMethodNames.Should().NotContain(
            "CompensateBy",
            "CompensationScope",
            "Compensate");
    }

    private static EphemeralWorkflowDefinition<string> BuildMinimal(
        DefinitionId definitionId) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Then<TestStep>()
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<string> BuildWithDelay(
        DefinitionId definitionId,
        TimeSpan delay) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState("created"))
            .Delay(delay)
            .End()
            .Build();

    private static string[] DeclaredMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private static void AssertCompletionStage(Type type) =>
        DeclaredMethods(type).Should().Equal("Build", "TryBuild");

    private sealed record TestState(string CorrelationId, bool ShouldRoute = true);

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
