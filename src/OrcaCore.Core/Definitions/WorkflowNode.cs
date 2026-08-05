namespace OrcaCore.Core.Definitions;

internal abstract record WorkflowNode<TState>
{
    protected WorkflowNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        NodeId = nodeId;
    }

    public string NodeId { get; }
}
