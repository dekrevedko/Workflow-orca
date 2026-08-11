using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

/// <summary>Provides one grouped durable workflow count for host and operator diagnostics.</summary>
public sealed record WorkflowOperatorStatisticsGroup(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    global::OrcaCore.WorkflowInstanceStatus Status,
    long Count);

/// <summary>Provides one grouped stuck-instance count for host and operator diagnostics.</summary>
public sealed record WorkflowOperatorStuckGroup(
    DefinitionId DefinitionId,
    long Count);

/// <summary>Provides one grouped active-wait count for host and operator diagnostics.</summary>
public sealed record WorkflowOperatorActiveWaitGroup(
    DefinitionId DefinitionId,
    global::OrcaCore.EventName EventName,
    long Count);

/// <summary>Provides provider-authoritative durable storage and dispatch pressure.</summary>
public sealed record WorkflowOperationalPressure
{
    public long ActiveInstanceCount { get; init; }

    public long StuckInstanceCount { get; init; }

    public long ActiveWaitCount { get; init; }

    public long StreamEventCount { get; init; }

    public long CheckpointCount { get; init; }

    public long CheckpointLag { get; init; }

    public long ContinuationPendingCount { get; init; }

    public long ContinuationRetryableCount { get; init; }

    public long ContinuationClaimedCount { get; init; }

    public long ContinuationPoisonedCount { get; init; }

    public long ExternalOutboxPendingCount { get; init; }

    public long ExternalOutboxRetryableCount { get; init; }

    public long ExternalOutboxClaimedCount { get; init; }

    public long ExternalOutboxPoisonedCount { get; init; }
}

/// <summary>
/// Provides one immutable provider-authoritative snapshot used by both host/operator inspection
/// and BCL observable gauges.
/// </summary>
public sealed record WorkflowOperatorStatistics
{
    public required string ProviderName { get; init; }

    public required IReadOnlyList<WorkflowOperatorStatisticsGroup> Groups { get; init; }

    public required IReadOnlyList<WorkflowOperatorStuckGroup> StuckGroups { get; init; }

    public required IReadOnlyList<WorkflowOperatorActiveWaitGroup> ActiveWaitGroups { get; init; }

    public required WorkflowOperationalPressure Pressure { get; init; }
}

/// <summary>
/// Defines the provider-authoritative observation time and validated inactivity threshold for one
/// operational snapshot.
/// </summary>
public sealed record WorkflowOperatorStatisticsRequest
{
    /// <summary>Initializes one deterministic operational-snapshot request.</summary>
    public WorkflowOperatorStatisticsRequest(DateTimeOffset observedAt, TimeSpan stuckThreshold)
    {
        if (stuckThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stuckThreshold),
                stuckThreshold,
                "The stuck-detection threshold must be positive.");
        }

        try
        {
            _ = observedAt.Subtract(stuckThreshold);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stuckThreshold),
                stuckThreshold,
                "The stuck-detection threshold must produce a representable cutoff.");
        }

        ObservedAt = observedAt;
        StuckThreshold = stuckThreshold;
    }

    /// <summary>Gets the runtime-owned observation time.</summary>
    public DateTimeOffset ObservedAt { get; }

    /// <summary>Gets the positive host/operator inactivity threshold.</summary>
    public TimeSpan StuckThreshold { get; }
}

/// <summary>Exposes provider-authoritative operational projections without application enumeration.</summary>
public interface IWorkflowOperationalStore
{
    /// <summary>Persists stuck-instance observations for the supplied provider-authoritative cutoff.</summary>
    Task RefreshStuckStateAsync(
        WorkflowOperatorStatisticsRequest request,
        CancellationToken cancellationToken);

    /// <summary>Captures one immutable grouped statistics and pressure snapshot.</summary>
    Task<WorkflowOperatorStatistics> GetOperatorStatisticsAsync(
        CancellationToken cancellationToken);
}
