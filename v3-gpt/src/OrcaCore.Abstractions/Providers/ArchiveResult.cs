namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Describes the outcome of a retention archive attempt.
/// </summary>
public sealed record ArchiveResult
{
    /// <summary>
    /// Gets whether instance metadata was marked archived.
    /// </summary>
    public required bool Archived { get; init; }

    /// <summary>
    /// Gets a human-readable reason when archive was rejected.
    /// </summary>
    public string? Reason { get; init; }
}
