using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides an immutable metadata-only view of one active runtime-owned wait.
/// </summary>
public sealed record ActiveWaitSnapshot
{
    /// <summary>
    /// Gets the runtime wait identity.
    /// </summary>
    public required WaitId WaitId { get; init; }

    /// <summary>
    /// Gets the event name this wait matches.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the correlation identity this wait matches.
    /// </summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>
    /// Gets when the wait was registered.
    /// </summary>
    public required DateTimeOffset RegisteredAt { get; init; }

    /// <summary>
    /// Gets the branch identity when the wait belongs to a parallel branch.
    /// </summary>
    public string? BranchId { get; init; }

    /// <summary>
    /// Gets the wait status name.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets the wait residency mode name.
    /// </summary>
    public required string Mode { get; init; }

    /// <summary>
    /// Gets the logical fiber that owns the wait when available.
    /// </summary>
    public FiberId? FiberId { get; init; }

    /// <summary>
    /// Gets the structured scope that owns the wait when available.
    /// </summary>
    public ScopeId? ScopeId { get; init; }

    /// <summary>
    /// Gets the persisted per-instance registration order used for deterministic matching.
    /// </summary>
    public long WaitSequence { get; init; }
}
