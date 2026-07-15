using OrcaCore.Abstractions.Errors;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private StructuredExecutionState MergeAndResume(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        ExecutionScopeRecord scope,
        WorkflowInstance<TState> instance)
    {
        try
        {
            var scopePlan = plan.GetScope(scope.ScopePlanId);
            var parent = state.Fibers[scope.ParentFiberId];
            var parentState = ResolveFiberState(plan, state, parent, instance.State);
            StructuredSerializedValue replacementPayload;
            if (scope.Kind == CompiledScopeKind.ForEach)
            {
                var runtime = scope.ForEach ??
                    throw new WorkflowDefinitionException("ForEach scope runtime state is missing.");
                var outcomes = runtime.Outcomes.Values.AsEnumerable();
                if (runtime.JoinPolicy == ForEachJoinPolicy.WhenAny)
                {
                    var winner = scope.WinnerFiberId ??
                        throw new WorkflowDefinitionException("ForEach WhenAny scope has no winner.");
                    var winnerIndex = runtime.ItemIndexByFiber[winner];
                    outcomes = outcomes.Where(outcome => outcome.Index == winnerIndex);
                }

                replacementPayload = ScopeMergeAdapter.ExecuteForEach(
                    scopePlan,
                    parentState,
                    outcomes.ToArray(),
                    codec);
            }
            else
            {
                var materializedResults = scopePlan.Branches
                    .OrderBy(branch => branch.Ordinal)
                    .Where(branch => scope.Kind != CompiledScopeKind.WhenFirst ||
                        scope.ChildFiberIds[branch.Ordinal] == scope.WinnerFiberId)
                    .Select(branch =>
                    {
                        var childId = scope.ChildFiberIds[branch.Ordinal];
                        var payload = scope.CommittedResults[childId] ??
                            throw new WorkflowDefinitionException(
                                "Committed branch result payload is missing.");
                        return new MaterializedBranchResult(
                            branch.Id,
                            new StructuredSerializedValue(
                                branch.Result.ResultType,
                                branch.Result.ResultSchemaIdentity,
                                payload));
                    })
                    .ToArray();
                replacementPayload = ScopeMergeAdapter.Execute(
                    scopePlan,
                    parentState,
                    materializedResults,
                    codec);
            }

            var replacement = codec.Deserialize(replacementPayload);
            if (scope.ParentFiberId == state.RootFiberId && replacement is not TState)
            {
                throw new WorkflowDefinitionException(
                    $"Structured merge did not produce '{typeof(TState).FullName}'.");
            }

            var merging = ScopeReducer.BeginMerge(state, scope.Id);
            var completed = ScopeReducer.CompleteMerge(
                plan,
                merging,
                scope.Id,
                replacementPayload.Payload);
            if (scope.ParentFiberId == state.RootFiberId)
            {
                instance.ReplaceState((TState)replacement!);
            }

            return completed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return FailMergeBoundary(state, scope, instance, exception);
        }
    }
}
