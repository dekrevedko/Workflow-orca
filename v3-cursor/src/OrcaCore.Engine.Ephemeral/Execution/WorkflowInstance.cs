using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Runtime state for one workflow execution: engine-owned metadata (status, execution
/// pointer, error, end outcome, active wait, timestamps) plus the workflow-owned
/// <typeparamref name="TState"/> (CR-020). Never exposed publicly (CR-021) —
/// <see cref="ToSnapshot"/> is the only projection.
/// </summary>
/// <typeparam name="TState">Workflow-owned business state type.</typeparam>
internal sealed class WorkflowInstance<TState>
{
    internal WorkflowInstance(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TState state,
        DateTimeOffset createdAt)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        State = state;
        Status = WorkflowStatus.Running;
        Pointer = ExecutionPointer.Empty;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    internal InstanceId InstanceId { get; }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    /// <summary>Mutable business state. Steps mutate this object directly (CR-011).</summary>
    internal TState State { get; }

    internal WorkflowStatus Status { get; private set; }

    internal ExecutionPointer Pointer { get; private set; }

    internal DateTimeOffset CreatedAt { get; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal StepErrorDetails? Error { get; private set; }

    internal string? EndOutcomeName { get; private set; }

    /// <summary>
    /// The instance's single resident wait record (EV-021, EV-040), or <see langword="null"/>
    /// when not suspended on an event. One wait at a time is sufficient for T1-08's scope
    /// (instance-local, single-threaded straight-line/If/While execution); per-branch waits
    /// arrive with <see cref="ParallelNode"/> support in T1-12.
    /// </summary>
    internal WaitRecord? ActiveWait { get; private set; }

    /// <summary>
    /// Per-<see cref="WhileNode"/> iteration counters so each loop body entry gets a distinct
    /// <see cref="Frame.LoopIteration"/> (EV-043). Owned by the interpreter; not part of snapshots.
    /// </summary>
    internal Dictionary<WhileNode, int> LoopIterationCounters { get; } = [];

    /// <summary>
    /// The single seam every runtime-state mutation flows through (position, status, error,
    /// end outcome). T1-06 wraps calls to this method with the per-instance execution lane
    /// (CR-040/041) without the interpreter needing to change.
    /// </summary>
    /// <param name="pointer">Execution position after this mutation.</param>
    /// <param name="status">Lifecycle status after this mutation (already validated by the caller via <c>LifecycleMachine</c>).</param>
    /// <param name="now">Timestamp of this mutation.</param>
    /// <param name="error">Failure details when this mutation is a failure transition; otherwise <see langword="null"/>.</param>
    /// <param name="endOutcomeName">Named end outcome when this mutation is a completion transition; otherwise <see langword="null"/>.</param>
    internal void Advance(
        ExecutionPointer pointer,
        WorkflowStatus status,
        DateTimeOffset now,
        StepErrorDetails? error = null,
        string? endOutcomeName = null)
    {
        Pointer = pointer;
        Status = status;
        UpdatedAt = now;
        Error = error ?? Error;
        EndOutcomeName = endOutcomeName ?? EndOutcomeName;
    }

    /// <summary>Registers <paramref name="wait"/> as this instance's resident wait record (EV-021).</summary>
    internal void RegisterWait(WaitRecord wait) => ActiveWait = wait;

    /// <summary>Clears the resident wait record after it matches (EV-023) or is cancelled (EV-044).</summary>
    internal void ClearActiveWait() => ActiveWait = null;

    /// <summary>Projects a metadata-only immutable snapshot (CR-021, CR-016).</summary>
    internal WorkflowInstanceSnapshot ToSnapshot() => new(
        InstanceId,
        DefinitionId,
        DefinitionVersion,
        Status,
        CreatedAt,
        UpdatedAt,
        Error?.Summary,
        EndOutcomeName,
        ActiveWait is null ? [] : [ActiveWait.ToSnapshot()]);
}
