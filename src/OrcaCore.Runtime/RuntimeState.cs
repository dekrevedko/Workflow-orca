using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal enum BranchStatus
{
    Running,
    Waiting,
    Completed,
    Failed
}

internal sealed class RuntimeState
{
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public int ExecutionPointer { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastTransitionAt { get; set; } = DateTimeOffset.UtcNow;
    public List<WaitRecord> ActiveWaits { get; } = [];
    public List<PendingEvent> PendingEvents { get; } = [];
    public HashSet<string> ConsumedEventIds { get; } = [];
    public WorkflowError? Error { get; set; }
    public ParallelExecutionState? ActiveParallel { get; set; }
    /// <summary>
    /// Stack of positions within nested step lists (While/If bodies).
    /// Used to resume at the correct position after a Wait inside a nested structure.
    /// </summary>
    public Stack<int> NestedPointers { get; } = new();
}

internal sealed class ParallelExecutionState
{
    public Dictionary<string, BranchStatus> BranchStatuses { get; } = new();
    public Dictionary<string, int> BranchPointers { get; } = new();
}
