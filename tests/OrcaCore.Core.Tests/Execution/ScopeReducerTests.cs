using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class ScopeReducerTests
{
    [Fact]
    public void StartScope_PreservesAndBlocksParent_AndCreatesChildrenInAuthoredOrder()
    {
        var plan = ParallelPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(
            InstanceId.New(),
            generation: 0,
            startInstruction.Id);
        var originalParent = initial.Fibers[initial.RootFiberId];

        var transition = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);

        var parent = transition.State.Fibers[initial.RootFiberId];
        parent.InstructionId.Should().Be(originalParent.InstructionId);
        parent.Phase.Should().Be(FiberPhase.Blocked);
        parent.Blocked.Should().Be(new FiberBlock(
            FiberBlockedReason.Scope,
            transition.ScopeId.Value));
        parent.NextScopeEntrySequence.Should().Be(1);

        var scope = transition.State.Scopes[transition.ScopeId];
        scope.Phase.Should().Be(ExecutionScopePhase.Running);
        scope.ParentFiberId.Should().Be(initial.RootFiberId);
        scope.ParentScopeId.Should().BeNull();
        scope.ScopeEntrySequence.Should().Be(0);
        scope.ChildFiberIds.Should().Equal(transition.ChildFiberIds);

        transition.ChildFiberIds.Should().HaveCount(2);
        transition.ChildFiberIds.Select(id => transition.State.Fibers[id].InstructionId)
            .Should().Equal(scopePlan.Branches.Select(branch => branch.Instructions[0]));
        transition.ChildFiberIds.Select(id => transition.State.Fibers[id].OwningScopeId)
            .Should().OnlyContain(id => id == transition.ScopeId);
        transition.State.Scheduler.RunnableFiberIds.Should().Equal(transition.ChildFiberIds);
        transition.State.Scheduler.NextFiberId.Should().Be(transition.ChildFiberIds[0]);
    }

    [Fact]
    public void ScopeLifecycle_UsesValidatedJoinMergeAndTerminalTransitions()
    {
        var plan = ParallelPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);
        var running = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan)
            .State.Scopes.Values.Should().ContainSingle().Which;

        var joinable = ScopeReducer.Transition(running, ExecutionScopePhase.Joinable);
        var merging = ScopeReducer.Transition(joinable, ExecutionScopePhase.Merging);
        var completed = ScopeReducer.Transition(merging, ExecutionScopePhase.Completed);
        var failed = ScopeReducer.Transition(running, ExecutionScopePhase.Failed);
        var cancelled = ScopeReducer.Transition(running, ExecutionScopePhase.Cancelled);
        var invalid = () => ScopeReducer.Transition(running, ExecutionScopePhase.Completed);

        completed.Phase.Should().Be(ExecutionScopePhase.Completed);
        failed.Phase.Should().Be(ExecutionScopePhase.Failed);
        cancelled.Phase.Should().Be(ExecutionScopePhase.Cancelled);
        invalid.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ScopeStart_IsReplayDeterministic_AndAppliesRecursivelyToChildFibers()
    {
        var plan = ParallelPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);

        var firstHost = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var replacementHost = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var nested = ScopeReducer.StartScope(
            firstHost.State,
            firstHost.ChildFiberIds[0],
            scopePlan);

        replacementHost.ScopeId.Should().Be(firstHost.ScopeId);
        replacementHost.ChildFiberIds.Should().Equal(firstHost.ChildFiberIds);
        var nestedScope = nested.State.Scopes[nested.ScopeId];
        nestedScope.ParentScopeId.Should().Be(firstHost.ScopeId);
        nestedScope.ParentFiberId.Should().Be(firstHost.ChildFiberIds[0]);
        nested.ScopeId.Should().NotBe(firstHost.ScopeId);
        nested.ChildFiberIds.Should().NotIntersectWith(firstHost.ChildFiberIds);
    }

    [Fact]
    public async Task JoinAndExit_AreReducerTransitions_ThatResumeParentAfterScopeExit()
    {
        var plan = ParallelPlan();
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var first = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            started.State,
            new ReturningExecutor(),
            TestContext.Current.CancellationToken);
        var second = await ReferenceLinearFiberInterpreter.RunQuantumAsync(
            plan,
            first.State,
            new ReturningExecutor(),
            TestContext.Current.CancellationToken);
        second.State.Scopes[started.ScopeId].Phase.Should().Be(ExecutionScopePhase.Joinable);
        second.State.Scheduler.NextFiberId.Should().BeNull();

        var merging = ScopeReducer.BeginMerge(second.State, started.ScopeId);
        var completed = ScopeReducer.CompleteMerge(
            plan,
            merging,
            started.ScopeId,
            parentStatePayload: [4, 5, 6]);

        completed.Scopes.Should().NotContainKey(started.ScopeId);
        completed.Fibers.Keys.Should().NotIntersectWith(started.ChildFiberIds);
        var parent = completed.Fibers[initial.RootFiberId];
        parent.Phase.Should().Be(FiberPhase.Runnable);
        parent.InstructionId.Should().Be(plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.End).Which.Id);
        parent.LocalStatePayload.Should().Equal(4, 5, 6);
        completed.Scheduler.NextFiberId.Should().Be(initial.RootFiberId);
    }

    private static CompiledWorkflowPlan ParallelPlan()
    {
        return Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value))
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>("first", parent => new BranchState(parent.Value.Value), branch =>
                        branch.Return(state => state.Value.Value))
                    .Branch<BranchState>("second", parent => new BranchState(parent.Value.Value), branch =>
                        branch.Return(state => state.Value.Value)),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
    }

    private sealed class ReturningExecutor : ICompiledInstructionExecutor
    {
        public ValueTask<InstructionExecutionResult> ExecuteAsync(
            CompiledInstruction instruction,
            FiberRecord fiber,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<InstructionExecutionResult>(
                new InstructionExecutionResult.BranchReturn([1]));
        }
    }

    private sealed record ParentState(string Value);

    private sealed record BranchState(string Value);
}
