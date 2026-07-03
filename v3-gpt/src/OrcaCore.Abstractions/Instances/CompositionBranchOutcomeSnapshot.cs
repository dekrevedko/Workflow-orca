namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides an immutable metadata-only view of one branch outcome inside a composition node.
/// </summary>
public sealed record CompositionBranchOutcomeSnapshot
{
    /// <summary>
    /// Gets the composition node identity that produced the outcome.
    /// </summary>
    public required string CompositionId { get; init; }

    /// <summary>
    /// Gets the branch identity in ordinal:name form.
    /// </summary>
    public required string BranchId { get; init; }

    /// <summary>
    /// Gets the branch outcome status name.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Gets when the branch outcome was recorded.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }
}
