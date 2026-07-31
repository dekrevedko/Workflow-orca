using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

/// <summary>
/// Consumer-facing mode-first authoring coverage. Assertions deliberately stop at
/// public definition metadata, diagnostics, and API shape; compiled-plan inspection
/// belongs to the implementation-tier compiler suite.
/// The former child-workflow lowering declaration remains a Section 8 obligation:
/// public child authoring is intentionally absent from the v1 surface.
/// </summary>
public sealed class ModeFirstWorkflowBuilderTests
{
    [Fact]
    [Trait("AC", "AC-016")]
    public void Build_AndTryBuild_UseTheSamePublicDefinitionContract()
    {
        var definitionId = DefinitionId.New();
        var completion = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End(WorkflowOutcomeName.Create("completed"));

        var validation = completion.TryBuild();
        var definition = completion.Build();

        validation.IsValid.Should().BeTrue();
        validation.TryGetValue(out var validated).Should().BeTrue();
        validated!.DefinitionFingerprint.Should().Be(definition.DefinitionFingerprint);
        definition.DefinitionId.Should().Be(definitionId);
        definition.DefinitionVersion.Should().Be(DefinitionVersion.Initial);
    }

    [Fact]
    [Trait("AC", "AC-017")]
    public void Parallel_EachBranchHasOneReachableTypedReturn()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create("first"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "first"))
                    .Branch(
                        AuthoredBranchId.Create("second"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "second")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parallel_MissingAndMultipleBranchReturns_AreRejected()
    {
        var missing = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches.Branch(
                    AuthoredBranchId.Create("missing"),
                    _ => new BranchState(),
                    _ => { }))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .TryBuild();

        var multiple = Workflow.Ephemeral<TestState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches.Branch(
                    AuthoredBranchId.Create("multiple"),
                    _ => new BranchState(),
                    branch => branch
                        .Return(_ => "first")
                        .Return(_ => "second")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .TryBuild();

        missing.IsValid.Should().BeFalse();
        missing.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-AUTH-BRANCH-002");
        multiple.IsValid.Should().BeFalse();
        multiple.Diagnostics.Should().Contain(
            diagnostic => diagnostic.Code == "SFE-AUTH-BRANCH-003");
    }

    [Fact]
    public void ContinueAsNew_IsDurableAndRootOnlyByPublicApiShape()
    {
        var validation = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(ShouldRollover: true))
            .ContinueAsNew(state => state.Value with { ShouldRollover = false })
            .TryBuild();

        validation.IsValid.Should().BeTrue();
        DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>))
            .Should().Contain("ContinueAsNew");
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().NotContain("ContinueAsNew");
        DeclaredMethods(typeof(DurableBranchBuilder<BranchState, string>))
            .Should().NotContain("ContinueAsNew");
    }

    [Fact]
    public void ContinueAsNew_IsATerminalCompletionStage()
    {
        var completion = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .ContinueAsNew(state => state.Value);

        DeclaredMethods(completion.GetType()).Should().Equal("Build", "TryBuild");
        completion.TryBuild().IsValid.Should().BeTrue();
    }

    [Fact]
    public void StructuralWaitAndDelay_AreCapabilityCorrectAndWaitLongIsAbsent()
    {
        var definitionId = DefinitionId.New();
        var correlation = CorrelationId.Create("order-42");
        var baseline = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End()
            .Build();
        var ephemeral = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(EventName.Create("ready"), _ => correlation)
            .Delay(TimeSpan.FromSeconds(5))
            .End()
            .Build();
        var durable = Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(EventName.Create("approved"), _ => correlation)
            .End()
            .Build();

        ephemeral.DefinitionFingerprint.Should().NotBe(baseline.DefinitionFingerprint);
        durable.Mode.Should().Be(WorkflowMode.Durable);
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().Contain(["Wait", "Delay"]).And.NotContain("WaitLong");
        DeclaredMethods(typeof(DurableWorkflowBuilder<string, TestState>))
            .Should().Contain(["Wait", "Delay"]).And.NotContain("WaitLong");
    }

    [Fact]
    public void BranchStructuralWaitAndDelay_ContributeToThePublicFingerprint()
    {
        var definitionId = DefinitionId.New();
        var baseline = BuildParallel(definitionId, branch => branch.Return(_ => "done"));
        var configured = BuildParallel(
            definitionId,
            branch => branch
                .Wait(
                    EventName.Create("branch-ready"),
                    _ => CorrelationId.Create("branch"))
                .Delay(TimeSpan.FromSeconds(1))
                .Return(_ => "done"));

        configured.DefinitionFingerprint.Should().NotBe(baseline.DefinitionFingerprint);
    }

    [Fact]
    public void InvalidBranchIdentityAndDuplicateBranches_ReportPublicDiagnostics()
    {
        Action blank = () => AuthoredBranchId.Create(" ");
        var duplicate = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches
                    .Branch(
                        AuthoredBranchId.Create("same"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "first"))
                    .Branch(
                        AuthoredBranchId.Create("same"),
                        _ => new BranchState(),
                        branch => branch.Return(_ => "second")))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .TryBuild();

        blank.Should().Throw<ArgumentException>();
        duplicate.IsValid.Should().BeFalse();
        duplicate.Diagnostics.Should().ContainSingle(
            diagnostic => diagnostic.Code == "SFE-AUTH-BRANCH-001");
    }

    [Fact]
    public void SelectedStepPolicies_ContributeToThePublicFingerprint()
    {
        var definitionId = DefinitionId.New();
        var configured = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then<TestStep>()
            .WithRetry(3, TimeSpan.FromSeconds(2))
            .WithStepTimeout(TimeSpan.FromSeconds(30))
            .WithTransientPool(TransientPoolName.Create("cpu"))
            .End()
            .Build();
        var baseline = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then<TestStep>()
            .End()
            .Build();

        configured.DefinitionFingerprint.Should().NotBe(baseline.DefinitionFingerprint);
    }

    [Fact]
    public void DurableBuilders_DoNotExposeTransientPoolAuthoring_WhileEveryEphemeralStepBuilderDoes()
    {
        Type[] durableBuilders =
        [
            typeof(DurableWorkflowBuilder<string, TestState>),
            typeof(DurableNestedBuilder<string, TestState>),
            typeof(DurableBranchBuilder<BranchState, string>),
            typeof(DurableItemBuilder<BranchState, string>),
            typeof(DurableLeaseWorkflowBuilder<string, TestState>),
            typeof(DurableLeaseNestedBuilder<string, TestState>),
            typeof(DurableLeaseBranchBuilder<BranchState, string>),
            typeof(DurableLeaseItemBuilder<BranchState, string>)
        ];
        Type[] ephemeralBuilders =
        [
            typeof(EphemeralWorkflowBuilder<string, TestState>),
            typeof(EphemeralNestedBuilder<string, TestState>),
            typeof(EphemeralBranchBuilder<BranchState, string>),
            typeof(EphemeralItemBuilder<BranchState, string>)
        ];

        durableBuilders.Should().OnlyContain(type =>
            type.GetMethod("WithTransientPool", BindingFlags.Instance | BindingFlags.Public) == null);
        ephemeralBuilders.Should().OnlyContain(type =>
            type.GetMethod("WithTransientPool", BindingFlags.Instance | BindingFlags.Public) != null);
    }

    [Fact]
    public void DurableStructuredBranch_CannotAuthorTransientPoolPolicy()
    {
        DeclaredMethods(typeof(DurableBranchBuilder<BranchState, string>))
            .Should().NotContain("WithTransientPool");
        DeclaredMethods(typeof(DurableItemBuilder<BranchState, string>))
            .Should().NotContain("WithTransientPool");
    }

    [Fact]
    public void Build_IsAvailableOnlyAfterARepresentableTerminalGraph()
    {
        DeclaredMethods(typeof(EphemeralWorkflowInitBuilder<TestState>))
            .Should().Equal("Init");
        DeclaredMethods(typeof(EphemeralWorkflowBuilder<string, TestState>))
            .Should().NotContain(["Build", "TryBuild", "Init"]);
        DeclaredMethods(typeof(EphemeralWorkflowCompletionBuilder<string>))
            .Should().Equal("Build", "TryBuild");
    }

    private static EphemeralWorkflowDefinition<string> BuildParallel(
        DefinitionId definitionId,
        Action<EphemeralBranchBuilder<BranchState, string>> body) =>
        Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches.Branch(
                    AuthoredBranchId.Create("worker"),
                    _ => new BranchState(),
                    body))
            .WhenAll((parent, _) => parent.Value)
            .End()
            .Build();

    private static string[] DeclaredMethods(Type type) => type
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(method => method.Name)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    private sealed record TestState(bool ShouldRollover = false);

    private sealed record BranchState;

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
