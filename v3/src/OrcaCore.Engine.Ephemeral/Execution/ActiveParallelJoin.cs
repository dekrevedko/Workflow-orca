using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Runtime state for one in-flight <see cref="ParallelNode"/> (CP-002 WhenAll join): the node
/// itself plus one <see cref="BranchRuntime"/> per declared branch, in declaration order. While
/// this is set on <see cref="WorkflowInstance{TState}.ActiveJoin"/>, the outer instance pointer
/// stays parked at the <see cref="ParallelNode"/>'s own position — it is only advanced past the
/// node once the join fires (all branches <see cref="WorkflowStatus.Completed"/>), which happens
/// exactly once (CP-002) because <see cref="ActiveParallelJoin"/> is cleared in the same step that
/// advances the outer pointer.
/// </summary>
internal sealed class ActiveParallelJoin(ParallelNode node, IReadOnlyList<BranchRuntime> branches)
{
    public ParallelNode Node { get; } = node;

    /// <summary>One runtime per <see cref="ParallelNode.Branches"/> entry, in the SAME (declaration) order — CP-003 determinism relies on always walking this list in this order.</summary>
    public IReadOnlyList<BranchRuntime> Branches { get; } = branches;
}
