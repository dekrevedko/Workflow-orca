using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Runtime-owned wait record (EV-021). Timeout is out of scope (T1-10+) and simply absent.
/// <see cref="BranchId"/> carries CP-001 branch identity when this wait was registered inside a
/// <see cref="ParallelNode"/> branch (T1-12) — null for instance-targeted (non-branch) waits, so
/// branch-scoped matching (EV-021/AC-110) stays isolated from the top-level wait and from other
/// branches. Internal — never exposed publicly (CR-021); callers observe waits only through
/// snapshot-shaped inspection surfaces.
/// </summary>
internal sealed class ActiveWait(
    WaitId waitId,
    string eventName,
    CorrelationId correlationId,
    DateTimeOffset registeredAt,
    BranchId? branchId = null)
{
    public WaitId WaitId { get; } = waitId;

    public string EventName { get; } = eventName;

    public CorrelationId CorrelationId { get; } = correlationId;

    public DateTimeOffset RegisteredAt { get; } = registeredAt;

    public WaitStatus Status { get; set; } = WaitStatus.Active;

    /// <summary>CP-001: the branch this wait belongs to, or null for a top-level instance wait.</summary>
    public BranchId? BranchId { get; } = branchId;
}
