namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides grouped workflow instance statistics for a management query.
/// </summary>
public sealed record WorkflowStatistics
{
    /// <summary>
    /// Gets the grouped counts.
    /// </summary>
    public required IReadOnlyList<WorkflowStatisticsGroup> Groups { get; init; }

    /// <summary>
    /// Gets provider pressure indicators.
    /// </summary>
    public WorkflowPressureMetrics Pressure { get; init; } = new();
}
