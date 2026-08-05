using System.Reflection;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private BranchTerminalTransition ReturnBranch(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "BranchReturn was reached outside an execution scope.");
        var scope = state.Scopes[scopeId];
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        var branch = ResolveBranch(plan, state, fiber);
        var localPayload = new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "Branch state payload is missing."));
        var localState = codec.Deserialize(localPayload);
        var snapshot = StructuredInvocationCache.CreateBranchSnapshot(
            branch.Result.BranchStateType,
            localState ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "Branch state deserialized to null."));
        object? result;
        try
        {
            result = StructuredInvocationCache.Invoke(branch.Result.Projector, snapshot);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                "Branch return projection failed.", exception.InnerException);
        }

        var resultPayload = codec.Serialize(
            result,
            branch.Result.ResultType,
            branch.Result.ResultSchemaIdentity);
        EnsureSerializedResultSize(plan, resultPayload.Payload);
        if (scope.Kind == CompiledScopeKind.ForEach)
        {
            var transition = ScopeReducer.RecordForEachTerminal(
                state,
                scopePlan,
                scopeId,
                fiber.Id,
                resultPayload.Payload,
                failure: null,
                maxConcurrentExecutionPathsPerInstance);
            return new BranchTerminalTransition(
                transition.State,
                transition.ScopeId,
                transition.ScopeBecameJoinable);
        }

        var returned = ScopeReducer.RecordBranchReturn(state, fiber, resultPayload.Payload);
        return new BranchTerminalTransition(
            returned.State,
            returned.ScopeId,
            returned.ScopeBecameJoinable);
    }
}
