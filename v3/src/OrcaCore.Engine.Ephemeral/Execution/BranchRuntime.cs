using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Live runtime state of one <see cref="ParallelBranch"/> (CP-001): its own position within
/// <see cref="ParallelBranch.Body"/>, its own status, and its own branch-scoped wait, entirely
/// isolated from the outer instance and from every sibling branch. Driven sequentially by the
/// interpreter inside the same lane-protected call as the outer run loop (CR-044) — never on a
/// separate <see cref="Task"/> — so no new synchronization primitive is needed: see T1-12
/// PROGRESS.md deviation note for why sequential round-robin satisfies CR-044/CP-002 without real
/// concurrency between branches.
/// </summary>
internal sealed class BranchRuntime(BranchId id)
{
    public BranchId Id { get; } = id;

    public ExecutionPointer Pointer { get; set; } = ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(0));

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;

    /// <summary>This branch's own resident wait (CP-001) — isolated from the instance's top-level wait and from sibling branches.</summary>
    public ActiveWait? ActiveWait { get; set; }

    /// <summary>Set when <see cref="Status"/> becomes <see cref="WorkflowStatus.Failed"/> — the error that failed this branch.</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>
    /// The envelope that resumed this branch, surfaced to its next step's
    /// <see cref="OrcaCore.Abstractions.Steps.StepContext{TState}.ResumedEvent"/> only (EV-022,
    /// branch-scoped). Cleared immediately after that one step executes.
    /// </summary>
    public EventEnvelope? PendingResumedEvent { get; set; }
}
