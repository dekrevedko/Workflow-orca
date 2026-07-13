namespace OrcaCore.Runtime.Execution.Nodes;

internal sealed class ParallelBranch<TState>(string branchId, IReadOnlyList<IWorkflowNode> nodes)
{
    public string BranchId { get; } = branchId;
    public IReadOnlyList<IWorkflowNode> Nodes { get; } = nodes;
}

internal sealed class ParallelNode<TState>(IReadOnlyList<ParallelBranch<TState>> branches) : IWorkflowNode
{
    public string NodeId => "Parallel";
    public IReadOnlyList<ParallelBranch<TState>> Branches { get; } = branches;
}
