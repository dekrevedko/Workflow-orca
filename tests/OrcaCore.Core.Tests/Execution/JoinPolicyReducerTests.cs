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
    public void WhenAllFailure_FailsFastAndCancelsEveryNonterminalSibling()
    {
        var plan = ScopePlan(whenFirst: false);
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var failure = new FiberFailure("branch-failed", "Branch failed.");

        var result = ScopeReducer.RecordChildTerminals(
            started.State,
            started.ScopeId,
            [ChildTerminalOutcome.Failed(started.ChildFiberIds[0], failure)]);

        result.State.Scopes[started.ScopeId].Phase.Should().Be(ExecutionScopePhase.Failed);
        result.State.Fibers[started.ChildFiberIds[0]].Phase.Should().Be(FiberPhase.Failed);
        result.State.Fibers[started.ChildFiberIds[1]].Phase.Should().Be(FiberPhase.Cancelled);
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    [Fact]
    public void WhenFirstSameTransitionTie_SelectsAuthoredWinnerAndCancelsLoser()
    {
        var plan = ScopePlan(whenFirst: true);
        var scopePlan = plan.Scopes.Should().ContainSingle().Which;
        var startInstruction = plan.Instructions.Should().ContainSingle(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope).Which;
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);
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
        var initial = StructuredExecutionState.Create(InstanceId.New(), 0, startInstruction.Id);
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

    [Fact]
    public void WhenFirstWinner_CancelsLosingNestedScopesAndDescendantFibersPostOrder()
    {
        var plan = NestedWhenFirstPlan();
        var outerPlan = plan.Scopes.Single(scope => scope.Merge.ParentStateType == typeof(ParentState));
        var nestedPlan = plan.Scopes.Single(scope => scope.Merge.ParentStateType == typeof(BranchState));
        var initial = StructuredExecutionState.Create(
            InstanceId.New(),
            0,
            plan.Instructions.Single(instruction => instruction.Path == "root/1").Id);
        var outer = ScopeReducer.StartScope(initial, initial.RootFiberId, outerPlan);
        var winner = outer.ChildFiberIds[0];
        var loser = outer.ChildFiberIds[1];
        var waitingFibers = new Dictionary<FiberId, FiberRecord>(outer.State.Fibers)
        {
            [winner] = FiberReducer.Block(
                outer.State.Fibers[winner],
                FiberBlockedReason.Wait,
                "winner-wait")
        };
        var waiting = outer.State with
        {
            Fibers = waitingFibers,
            Scheduler = FiberScheduler.CompleteTurn(
                outer.State.Scheduler,
                winner,
                requeueSelected: false)
        };
        var nested = ScopeReducer.StartScope(waiting, loser, nestedPlan);

        var result = ScopeReducer.RecordChildTerminals(
            nested.State,
            outer.ScopeId,
            [ChildTerminalOutcome.Succeeded(winner, [1])]);

        result.State.Fibers[loser].Phase.Should().Be(FiberPhase.Cancelled);
        nested.ChildFiberIds.Select(id => result.State.Fibers[id].Phase)
            .Should().OnlyContain(phase => phase == FiberPhase.Cancelled);
        result.State.Scopes[nested.ScopeId].Phase.Should().Be(ExecutionScopePhase.Cancelled);
        result.State.Scheduler.NextFiberId.Should().BeNull();
    }

    private static CompiledWorkflowPlan ScopePlan(bool whenFirst)
    {
        var builder = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
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

    private static CompiledWorkflowPlan NestedWhenFirstPlan()
    {
        return Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new ParentState(value))
            .WhenFirst<string>(
                branches => branches
                    .Branch<BranchState>(
                        "winner",
                        parent => new BranchState(parent.Value.Value),
                        branch => branch.Return(state => state.Value.Value))
                    .Branch<BranchState>(
                        "loser",
                        parent => new BranchState(parent.Value.Value),
                        branch => branch
                            .Parallel<string>(
                                nested => nested
                                    .Branch<BranchState>(
                                        "nested-a",
                                        parent => new BranchState(parent.Value.Value),
                                        child => child.Return(state => state.Value.Value))
                                    .Branch<BranchState>(
                                        "nested-b",
                                        parent => new BranchState(parent.Value.Value),
                                        child => child.Return(state => state.Value.Value)),
                                (parent, _) => parent.Value)
                            .Return(state => state.Value.Value)),
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
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
