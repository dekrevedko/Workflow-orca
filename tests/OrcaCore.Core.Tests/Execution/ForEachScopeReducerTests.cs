using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class ForEachScopeReducerTests
{
    [Fact]
    public void DynamicAdmission_NeverExceedsConfiguredActiveFiberLimit()
    {
        var plan = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new ParentState([1, 2, 3]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Index),
                body => body.Return(item => item.Value.Index.ToString()),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.FailFast,
                merge: (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
        var scopePlan = plan.Scopes.Single();
        var start = plan.Instructions.Single(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope);
        var state = StructuredExecutionState.Create(InstanceId.New(), 0, start.Id);

        var started = ScopeReducer.StartForEachScope(
            state,
            state.RootFiberId,
            scopePlan,
            [
                new ForEachItemDescriptor(0, [0]),
                new ForEachItemDescriptor(1, [1]),
                new ForEachItemDescriptor(2, [2])
            ],
            maxActiveFibers: 2);
        var first = started.AdmittedFiberIds.Should().ContainSingle().Which;

        var transition = ScopeReducer.RecordForEachTerminal(
            started.State,
            scopePlan,
            started.ScopeId,
            first,
            [0],
            failure: null,
            maxActiveFibers: 2);

        transition.AdmittedFiberIds.Should().ContainSingle();
        transition.State.Fibers.Values.Count(fiber =>
                fiber.Phase is FiberPhase.Runnable or FiberPhase.Blocked)
            .Should().Be(2);
    }

    [Fact]
    public void WhenAnySameTransitionTie_SelectsLowestItemIndex()
    {
        var plan = Workflow.Ephemeral<ParentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int[]>(_ => new ParentState([1, 2]))
            .ForEach<int, ItemState, string>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ItemState(item.Index),
                body => body.Return(item => item.Value.Index.ToString()),
                ForEachJoinPolicy.WhenAny,
                ForEachFailurePolicy.FailFast,
                merge: (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
        var scopePlan = plan.Scopes.Single();
        var start = plan.Instructions.Single(instruction =>
            instruction.Kind == CompiledInstructionKind.StartScope);
        var state = StructuredExecutionState.Create(InstanceId.New(), 0, start.Id);
        var started = ScopeReducer.StartForEachScope(
            state,
            state.RootFiberId,
            scopePlan,
            [new ForEachItemDescriptor(0, [0]), new ForEachItemDescriptor(1, [1])]);
        var scope = started.State.Scopes[started.ScopeId];
        var itemZero = scope.ForEach!.ItemIndexByFiber.Single(pair => pair.Value == 0).Key;
        var itemOne = scope.ForEach.ItemIndexByFiber.Single(pair => pair.Value == 1).Key;

        var transition = ScopeReducer.RecordForEachTerminalBatch(
            started.State,
            scopePlan,
            started.ScopeId,
            [
                ChildTerminalOutcome.Succeeded(itemOne, [1]),
                ChildTerminalOutcome.Succeeded(itemZero, [0])
            ]);

        var joined = transition.State.Scopes[started.ScopeId];
        joined.Phase.Should().Be(ExecutionScopePhase.Joinable);
        joined.WinnerFiberId.Should().Be(itemZero);
        transition.State.Fibers[itemZero].Phase.Should().Be(FiberPhase.Completed);
        transition.State.Fibers[itemOne].Phase.Should().Be(FiberPhase.Cancelled);
    }

    private sealed record ParentState(IReadOnlyList<int> Items);

    private sealed record ItemState(int Index);
}
