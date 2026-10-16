namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class WhileNode<TState>(
    Func<TState, bool> condition,
    IReadOnlyList<IWorkflowNode> bodyNodes) : IWorkflowNode
{
    public string NodeId => "While";
    public Func<TState, bool> Condition { get; } = condition;
    public IReadOnlyList<IWorkflowNode> BodyNodes { get; } = bodyNodes;
}
