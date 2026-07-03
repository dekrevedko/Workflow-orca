namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides durable provider pressure indicators for operational monitoring.
/// </summary>
public sealed record WorkflowPressureMetrics
{
    /// <summary>
    /// Gets the total number of workflow stream events visible to the provider.
    /// </summary>
    public long TotalStreamEvents { get; init; }

    /// <summary>
    /// Gets the number of checkpoint records visible to the provider.
    /// </summary>
    public int CheckpointCount { get; init; }

    /// <summary>
    /// Gets the number of outbox records waiting to be dispatched.
    /// </summary>
    public int PendingOutboxCount { get; init; }

    /// <summary>
    /// Gets the number of projected non-terminal instances.
    /// </summary>
    public int ActiveInstanceCount { get; init; }
}
