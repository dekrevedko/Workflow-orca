namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class IfNode<TState>(
    Func<TState, bool> condition,
    IReadOnlyList<IWorkflowNode> thenNodes,
    IReadOnlyList<IWorkflowNode> elseNodes) : IWorkflowNode
{
    public string NodeId => "If";
    public Func<TState, bool> Condition { get; } = condition;
    public IReadOnlyList<IWorkflowNode> ThenNodes { get; } = thenNodes;
    public IReadOnlyList<IWorkflowNode> ElseNodes { get; } = elseNodes;
}
