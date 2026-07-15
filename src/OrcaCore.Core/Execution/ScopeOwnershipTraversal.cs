namespace OrcaCore.Core.Execution;

internal abstract record OwnedExecutionNode;

internal sealed record OwnedFiberNode(FiberId FiberId) : OwnedExecutionNode;

internal sealed record OwnedScopeNode(ScopeId ScopeId) : OwnedExecutionNode;

internal static class ScopeOwnershipTraversal
{
    internal static IReadOnlyList<OwnedExecutionNode> PostOrder(
        StructuredExecutionState state,
        IReadOnlyList<FiberId> rootFiberIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(rootFiberIds);

        var nodes = new List<OwnedExecutionNode>();
        var visitedFibers = new HashSet<FiberId>();
        var visitedScopes = new HashSet<ScopeId>();
        foreach (var fiberId in rootFiberIds)
        {
            VisitFiber(state, fiberId, visitedFibers, visitedScopes, nodes);
        }

        return nodes;
    }

    private static void VisitFiber(
        StructuredExecutionState state,
        FiberId fiberId,
        HashSet<FiberId> visitedFibers,
        HashSet<ScopeId> visitedScopes,
        List<OwnedExecutionNode> nodes)
    {
        if (!visitedFibers.Add(fiberId))
        {
            return;
        }

        foreach (var scope in state.Scopes.Values
                     .Where(scope =>
                         scope.ParentFiberId == fiberId &&
                         scope.Phase is not (
                             ExecutionScopePhase.Completed or
                             ExecutionScopePhase.Failed or
                             ExecutionScopePhase.Cancelled))
                     .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal))
        {
            VisitScope(state, scope, visitedFibers, visitedScopes, nodes);
        }

        nodes.Add(new OwnedFiberNode(fiberId));
    }

    private static void VisitScope(
        StructuredExecutionState state,
        ExecutionScopeRecord scope,
        HashSet<FiberId> visitedFibers,
        HashSet<ScopeId> visitedScopes,
        List<OwnedExecutionNode> nodes)
    {
        if (!visitedScopes.Add(scope.Id))
        {
            return;
        }

        foreach (var childFiberId in scope.ChildFiberIds)
        {
            VisitFiber(state, childFiberId, visitedFibers, visitedScopes, nodes);
        }

        nodes.Add(new OwnedScopeNode(scope.Id));
    }
}
