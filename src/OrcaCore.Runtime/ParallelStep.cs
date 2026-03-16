using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class ParallelBranch<TState>(string branchId, IReadOnlyList<IStep<TState>> steps)
{
    public string BranchId { get; } = branchId;
    public IReadOnlyList<IStep<TState>> Steps { get; } = steps;
}

internal sealed class ParallelStep<TState>(IReadOnlyList<ParallelBranch<TState>> branches) : IStep<TState>
{
    public string StepId => "Parallel";
    public IReadOnlyList<ParallelBranch<TState>> Branches { get; } = branches;

    public Task<StepResult> ExecuteAsync(StepContext<TState> context)
    {
        throw new NotSupportedException("ParallelStep is handled by the interpreter, not executed directly.");
    }
}
