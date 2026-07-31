using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class JoinPolicyReducerTests
{
    [Fact]
    public void WhenAllFailure_WaitsForEverySiblingWithoutCancellingRemainingWork()
    {
        var plan = ScopePlan(whenFirst: false);
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, startInstruction.Id);
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var failure = new FiberFailure("branch-failed", "Branch failed.");

        var afterFailure = ScopeReducer.RecordChildTerminals(
            started.State,
            started.ScopeId,
            [ChildTerminalOutcome.Failed(started.ChildFiberIds[0], failure)]);

        afterFailure.ScopeBecameJoinable.Should().BeFalse();
        afterFailure.State.Scopes[started.ScopeId].Phase.Should().Be(ExecutionScopePhase.Running);
        afterFailure.State.Fibers[started.ChildFiberIds[0]].Phase.Should().Be(FiberPhase.Failed);
        afterFailure.State.Fibers[started.ChildFiberIds[1]].Phase.Should().Be(FiberPhase.Runnable);
        afterFailure.State.Scheduler.NextFiberId.Should().Be(started.ChildFiberIds[1]);

        var afterSuccess = ScopeReducer.RecordChildTerminals(
            afterFailure.State,
            started.ScopeId,
            [ChildTerminalOutcome.Succeeded(started.ChildFiberIds[1], [2])]);

        afterSuccess.ScopeBecameJoinable.Should().BeFalse();
        afterSuccess.State.Scopes[started.ScopeId].Phase.Should().Be(ExecutionScopePhase.Failed);
        afterSuccess.State.Fibers[started.ChildFiberIds[0]].Phase.Should().Be(FiberPhase.Failed);
        afterSuccess.State.Fibers[started.ChildFiberIds[1]].Phase.Should().Be(FiberPhase.Completed);
        afterSuccess.State.Scheduler.NextFiberId.Should().BeNull();
    }

    [Fact]
    public void WhenAllFailure_PreservesOneFailureAndAggregatesMultipleInAuthoredOrder()
    {
        var first = new FiberFailure("FIRST", "First branch failed.");
        var second = new FiberFailure("SECOND", "Second branch failed.");

        ScopeReducer.AggregateFailures([first]).Should().BeSameAs(first);

        var aggregate = ScopeReducer.AggregateFailures([first, second]);

        aggregate.Code.Should().Be("SFE-JOIN-FAILED");
        aggregate.Causes.Should().Equal(first, second);
    }

    [Fact]
    public void WhenFirstSameTransitionTie_SelectsAuthoredWinnerAndCancelsLoser()
    {
        var plan = ScopePlan(whenFirst: true);
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, startInstruction.Id);
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);

        var result = ScopeReducer.RecordChildTerminals(
            started.State,
            started.ScopeId,
            [
                ChildTerminalOutcome.Succeeded(started.ChildFiberIds[1], [2]),
                ChildTerminalOutcome.Succeeded(started.ChildFiberIds[0], [1])
            ]);

        var scope = result.State.Scopes[started.ScopeId];
        scope.Phase.Should().Be(ExecutionScopePhase.Joinable);
        scope.WinnerFiberId.Should().Be(started.ChildFiberIds[0]);
        scope.CommittedResults.Keys.Should().Equal(started.ChildFiberIds[0]);
        result.State.Fibers[started.ChildFiberIds[0]].Phase.Should().Be(FiberPhase.Completed);
        result.State.Fibers[started.ChildFiberIds[1]].Phase.Should().Be(FiberPhase.Cancelled);
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    [Fact]
    public void WhenFirstFailedWinner_FailsScopeWithoutMergeResultAndCancelsLoser()
    {
        var plan = ScopePlan(whenFirst: true);
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.Parse(Guid.CreateVersion7().ToString()), 0, startInstruction.Id);
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var failedWinner = started.ChildFiberIds[1];

        var result = ScopeReducer.RecordChildTerminals(
            started.State,
            started.ScopeId,
            [ChildTerminalOutcome.Failed(
                failedWinner,
                new FiberFailure("winner-failed", "Winner failed."))]);

        var scope = result.State.Scopes[started.ScopeId];
        scope.Phase.Should().Be(ExecutionScopePhase.Failed);
        scope.WinnerFiberId.Should().Be(failedWinner);
        scope.CommittedResults.Should().BeEmpty();
        result.State.Fibers[failedWinner].Phase.Should().Be(FiberPhase.Failed);
        result.State.Fibers[started.ChildFiberIds[0]].Phase.Should().Be(FiberPhase.Cancelled);
    }

    private static CompiledWorkflowPlan ScopePlan(bool whenFirst)
    {
        var builder = global::OrcaCore.Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value));
        builder = whenFirst
            ? builder.WhenFirst<string>(
                Branches,
                (parent, _) => parent.Value)
            : builder.Parallel<string>(
                Branches,
                (parent, _) => parent.Value);
        return builder.End().Build().CompiledPlan;
    }

    private static void Branches(BranchScopeBuilder<ParentState, string> branches)
    {
        branches
            .Branch<BranchState>("first", parent => new BranchState(parent.Value.Value), branch =>
                branch.Return(state => state.Value.Value))
            .Branch<BranchState>("second", parent => new BranchState(parent.Value.Value), branch =>
                branch.Return(state => state.Value.Value));
    }

    private sealed record ParentState(string Value);

    private sealed record BranchState(string Value);
}
