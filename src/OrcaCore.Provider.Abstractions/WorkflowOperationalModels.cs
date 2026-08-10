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

/// <summary>Exposes provider-authoritative operational projections without application enumeration.</summary>
public interface IWorkflowOperationalStore
{
    /// <summary>Captures one immutable grouped statistics and pressure snapshot.</summary>
    Task<WorkflowOperatorStatistics> GetOperatorStatisticsAsync(CancellationToken cancellationToken);
}
