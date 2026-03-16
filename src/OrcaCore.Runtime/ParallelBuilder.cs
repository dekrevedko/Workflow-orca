namespace OrcaCore.Runtime;

public sealed class ParallelBuilder<TState>
{
    private readonly List<ParallelBranch<TState>> _branches = [];

    internal IReadOnlyList<ParallelBranch<TState>> GetBranches() => _branches.AsReadOnly();

    public ParallelBuilder<TState> Branch(string branchId, Action<BranchBuilder<TState>> configure)
    {
        var builder = new BranchBuilder<TState>();
        configure(builder);
        _branches.Add(new ParallelBranch<TState>(branchId, builder.GetSteps()));
        return this;
    }
}
