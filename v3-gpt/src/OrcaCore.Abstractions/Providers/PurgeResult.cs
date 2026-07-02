namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Describes the outcome of a retention purge attempt.
/// </summary>
public sealed record PurgeResult
{
    /// <summary>
    /// Gets whether instance data was purged.
    /// </summary>
    public required bool Purged { get; init; }

    /// <summary>
    /// Gets a human-readable reason when purge was rejected.
    /// </summary>
    public string? Reason { get; init; }
}
