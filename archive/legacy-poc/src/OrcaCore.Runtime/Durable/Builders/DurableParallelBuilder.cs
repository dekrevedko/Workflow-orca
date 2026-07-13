namespace OrcaCore.Runtime.Durable.Builders;

public sealed class DurableParallelBuilder<TState>
{
    private readonly List<ParallelBranch<TState>> _branches = [];

    internal IReadOnlyList<ParallelBranch<TState>> GetBranches() => _branches.AsReadOnly();

    public DurableParallelBuilder<TState> Branch(string branchId, Action<DurableBranchBuilder<TState>> configure)
    {
        var builder = new DurableBranchBuilder<TState>();
        configure(builder);
        _branches.Add(new ParallelBranch<TState>(branchId, builder.GetNodes()));
        return this;
    }
}
