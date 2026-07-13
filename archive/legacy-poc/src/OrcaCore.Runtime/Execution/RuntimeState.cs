
namespace OrcaCore.Runtime.Execution;

public enum FrameKind
{
    Root,
    IfBranch,
    WhileBody,
    ParallelBranch
}

internal sealed class ExecutionFrame(
    FrameKind kind,
    string nodePath,
    IReadOnlyList<IWorkflowNode> nodes,
    string? scopeId = null)
{
    public FrameKind Kind { get; } = kind;
    public string NodePath { get; } = nodePath;
    public IReadOnlyList<IWorkflowNode> Nodes { get; } = nodes;
    public int Index { get; set; }
    public string? ScopeId { get; } = scopeId;
}

internal sealed class ExecutionPath(string? branchId = null)
{
    public string? BranchId { get; } = branchId;
    public List<ExecutionFrame> Frames { get; } = [];

    public bool IsComplete => Frames.Count == 0;

    public ExecutionFrame CurrentFrame => Frames[^1];
}

internal sealed class ParallelFrameGroup
{
    public Dictionary<string, ExecutionPath> BranchPaths { get; } = [];
}

internal sealed class RuntimeState(
    DateTimeOffset? createdAt = null,
    DateTimeOffset? lastTransitionAt = null)
{
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public DateTimeOffset CreatedAt { get; } = createdAt ?? DateTimeOffset.UtcNow;
    public DateTimeOffset LastTransitionAt { get; set; } = lastTransitionAt ?? createdAt ?? DateTimeOffset.UtcNow;
    public List<WaitRecord> ActiveWaits { get; } = [];
    public List<PendingEvent> PendingEvents { get; } = [];
    public HashSet<string> ConsumedEventIds { get; } = [];
    public WorkflowError? Error { get; set; }
    public ExecutionPath MainPath { get; } = new();
    public ParallelFrameGroup? ActiveParallel { get; set; }
}
