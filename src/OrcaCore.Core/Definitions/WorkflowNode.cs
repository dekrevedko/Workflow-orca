namespace OrcaCore.Core.Definitions;

public abstract record WorkflowNode<TState>
{
    protected WorkflowNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        NodeId = nodeId;
    }

    public string NodeId { get; }
}
