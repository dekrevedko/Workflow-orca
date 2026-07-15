using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class ModeFirstWorkflowBuilderTests
{
    [Fact]
    [Trait("AC", "AC-016")]
    public void Build_AndTryBuild_UseTheSameCompiledPlanContract()
    {
        var definitionId = DefinitionId.New();
        var builder = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End("Completed");

        var validation = builder.TryBuild();
        var definition = builder.Build();

        validation.IsValid.Should().BeTrue();
        validation.Value.CompiledPlan.Fingerprint.Should().Be(definition.CompiledPlan.Fingerprint);
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
                    .Branch<BranchState>(
                        "first",
                        _ => new BranchState(),
                        branch => branch.Return(_ => "first"))
                    .Branch<BranchState>(
                        "second",
                        _ => new BranchState(),
                        branch => branch.Return(_ => "second")),
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Parallel_MissingAndMultipleBranchReturns_AreAccumulated()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("missing", _ => new BranchState(), _ => { })
                    .Branch<BranchState>(
                        "multiple",
                        _ => new BranchState(),
                        branch => branch
                            .Return(_ => "first")
                            .Return(_ => "second")),
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Contain(
            "SFE-AUTH-007_MISSING_BRANCH_RETURN",
            "SFE-AUTH-008_MULTIPLE_BRANCH_RETURN",
            "SFE-AUTH-009_UNREACHABLE_NODE");
    }

    [Fact]
    public void ContinueAsNew_IsStructuralDurableAndRootFiberOnlyByApiShape()
    {
        var validation = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState(ShouldRollover: true))
            .If(
                state => state.ShouldRollover,
                then => then.ContinueAsNew(state => state with { ShouldRollover = false }))
            .End()
            .TryBuild();

        validation.IsValid.Should().BeTrue();
        typeof(DurableWorkflowBuilder<TestState>).GetMethod("ContinueAsNew").Should().NotBeNull();
        typeof(EphemeralWorkflowBuilder<TestState>).GetMethod("ContinueAsNew").Should().BeNull();
        typeof(BranchBuilder<BranchState, string>).GetMethod("ContinueAsNew").Should().BeNull();
    }

    [Fact]
    public void ContinueAsNew_UnconditionallyBeforeEnd_ReportsUnreachableEnd()
    {
        var validation = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .ContinueAsNew(state => state)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(
            error => error.Code == "SFE-AUTH-009_UNREACHABLE_NODE");
    }

    [Fact]
    public void StructuralWaitDelayAndWaitLong_AreCapabilityCorrectAndCompiled()
    {
        var correlation = new CorrelationId("order-42");
        var ephemeral = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait("Ready", _ => correlation)
            .Delay(TimeSpan.FromSeconds(5))
            .End()
            .Build();
        var durable = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .WaitLong("Approved", _ => correlation)
            .End()
            .Build();

        var wait = ephemeral.CompiledPlan.Instructions.Single(instruction =>
            instruction.Kind == CompiledInstructionKind.Wait);
        wait.EventName.Should().Be("Ready");
        wait.WaitMode.Should().Be(WaitMode.Resident);
        ephemeral.CompiledPlan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.Delay &&
            instruction.DelayDuration == TimeSpan.FromSeconds(5));
        durable.CompiledPlan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.Wait &&
            instruction.EventName == "Approved" &&
            instruction.WaitMode == WaitMode.Cold);

        typeof(EphemeralWorkflowBuilder<TestState>).GetMethod("WaitLong").Should().BeNull();
        typeof(DurableWorkflowBuilder<TestState>).GetMethod("WaitLong").Should().NotBeNull();
    }

    [Fact]
    public void BranchStructuralWaitAndDelay_LowerInsideTheOwningFiber()
    {
        var plan = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "worker",
                    _ => new BranchState(),
                    branch => branch
                        .Wait("BranchReady", _ => new CorrelationId("branch"))
                        .Delay(TimeSpan.FromSeconds(1))
                        .Return(_ => "done")),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;

        plan.Instructions.Should().Contain(instruction =>
            instruction.Kind == CompiledInstructionKind.Wait &&
            instruction.Path.Contains("branches/0", StringComparison.Ordinal));
        plan.Instructions.Should().Contain(instruction =>
            instruction.Kind == CompiledInstructionKind.Delay &&
            instruction.Path.Contains("branches/0", StringComparison.Ordinal));
    }

    [Fact]
    public void ChildWorkflows_AreDurableOnlyStructuralInstructions()
    {
        var childDefinitionId = DefinitionId.New();
        var plan = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .RunChild(
                childDefinitionId,
                DefinitionVersion.Initial,
                RunChildFailurePolicy.ContinueParent)
            .RunChildren(
                childDefinitionId,
                DefinitionVersion.Initial,
                _ => ["first", "second"],
                maxConcurrency: 1,
                failurePolicy: RunChildFailurePolicy.PropagateFailure,
                joinPolicy: RunChildrenJoinPolicy.WhenAny,
                residualPolicy: RunChildrenResidualPolicy.CancelRemaining)
            .End()
            .Build()
            .CompiledPlan;

        plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.RunChild &&
            instruction.ChildDefinitionId == childDefinitionId &&
            instruction.ChildDefinitionVersion == DefinitionVersion.Initial &&
            instruction.ChildFailurePolicy == RunChildFailurePolicy.ContinueParent);
        plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.RunChildren &&
            instruction.ChildDefinitionId == childDefinitionId &&
            instruction.MaxConcurrency == 1 &&
            instruction.ChildJoinPolicy == RunChildrenJoinPolicy.WhenAny &&
            instruction.ChildResidualPolicy == RunChildrenResidualPolicy.CancelRemaining);
        typeof(DurableWorkflowBuilder<TestState>).GetMethod("RunChild").Should().NotBeNull();
        typeof(DurableWorkflowBuilder<TestState>).GetMethod("RunChildren").Should().NotBeNull();
        typeof(EphemeralWorkflowBuilder<TestState>).GetMethod("RunChild").Should().BeNull();
        typeof(EphemeralWorkflowBuilder<TestState>).GetMethod("RunChildren").Should().BeNull();
    }

    [Fact]
    public void TryBuild_BlankDuplicateBranchesAndMissingEnd_AccumulateInGraphOrder()
    {
        var validation = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("", _ => new BranchState(), branch => branch.Return(_ => "blank"))
                    .Branch<BranchState>("same", _ => new BranchState(), branch => branch.Return(_ => "first"))
                    .Branch<BranchState>("same", _ => new BranchState(), branch => branch.Return(_ => "second")),
                (parent, _) => parent.Value)
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            "SFE-AUTH-002_MISSING_ROOT_END",
            "SFE-AUTH-010_BLANK_BRANCH_IDENTITY",
            "SFE-AUTH-011_DUPLICATE_BRANCH_IDENTITY");
    }

    [Fact]
    public void SelectedStepPolicies_AreResolvedIntoPlanAndFingerprint()
    {
        var definitionId = DefinitionId.New();
        var configured = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .WithRetry(3, TimeSpan.FromSeconds(2))
            .WithTimeout(TimeSpan.FromSeconds(30))
            .WithCancellation()
            .WithPoolKey("cpu")
            .Then<TestStep>()
            .End()
            .Build();
        var baseline = Workflow.Ephemeral<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then<TestStep>()
            .End()
            .Build();

        var policy = configured.CompiledPlan.Instructions
            .Single(instruction => instruction.Kind == CompiledInstructionKind.Step)
            .Policy;
        policy.Retry.Should().Be(new CompiledRetryPolicy(3, TimeSpan.FromSeconds(2)));
        policy.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        policy.CancellationEnabled.Should().BeTrue();
        policy.TransientPoolKey.Should().Be("cpu");
        configured.CompiledPlan.Fingerprint.Should().NotBe(baseline.CompiledPlan.Fingerprint);
    }

    [Fact]
    public void DurableBuilder_DoesNotExposeTransientPoolAuthoring()
    {
        typeof(DurableWorkflowBuilder<TestState>)
            .GetMethod("WithPoolKey", BindingFlags.Instance | BindingFlags.Public)
            .Should().BeNull();
        typeof(EphemeralWorkflowBuilder<TestState>)
            .GetMethod("WithPoolKey", BindingFlags.Instance | BindingFlags.Public)
            .Should().NotBeNull();
    }

    [Fact]
    public void DurableStructuredBranch_RejectsTransientPoolPolicyAtCompilation()
    {
        var validation = Workflow.Durable<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "pooled",
                    _ => new BranchState(),
                    branch => branch
                        .WithPoolKey("cpu")
                        .Then<TestBranchStep>()
                        .Return(_ => "done")),
                (parent, _) => parent.Value)
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error =>
            error.Code == DefinitionCompilerCodes.UnsupportedInstruction &&
            error.Message.Contains("Transient", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_InvalidGraph_ThrowsAllCompilerDiagnostics()
    {
        var builder = Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Parallel<string>(
                branches => branches.Branch<BranchState>(
                    "missing-return",
                    _ => new BranchState(),
                    _ => { }),
                (parent, _) => parent.Value);

        var act = () => builder.Build();

        act.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain("SFE-AUTH-001_MISSING_ROOT_INIT")
            .And.Contain("SFE-AUTH-002_MISSING_ROOT_END")
            .And.Contain("SFE-AUTH-007_MISSING_BRANCH_RETURN");
    }

    private sealed record TestState(bool ShouldRollover = false);

    private sealed record BranchState;

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class TestBranchStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
