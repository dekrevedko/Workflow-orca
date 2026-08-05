using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Describes an explicit durable retention operation for one workflow instance.
/// </summary>
internal sealed record RetentionPolicy
{
    /// <summary>
    /// Gets the workflow instance targeted by the retention operation.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets when the retention decision was requested.
    /// </summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// Gets the operator or policy reason for the retention decision.
    /// </summary>
    public string? Reason { get; init; }
}
