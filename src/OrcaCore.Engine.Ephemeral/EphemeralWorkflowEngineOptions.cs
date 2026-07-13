using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Configures in-process operational behavior for the ephemeral workflow engine.
/// </summary>
public sealed record EphemeralWorkflowEngineOptions
{
    /// <summary>
    /// Gets the maximum number of unmatched events retained in one instance mailbox.
    /// Additional unmatched delivery is rejected instead of growing memory without bound.
    /// </summary>
    public int MaxPendingEventsPerInstance { get; init; } = 1_024;

    /// <summary>
    /// Gets the maximum number of consumed event ids retained for in-process deduplication.
    /// </summary>
    public int MaxConsumedEventIdsPerInstance { get; init; } = 10_000;

    /// <summary>
    /// Gets the maximum number of lifecycle audit entries retained per instance.
    /// Oldest entries are discarded after this in-memory limit is reached.
    /// </summary>
    public int MaxLifecycleEventsPerInstance { get; init; } = 10_000;

    /// <summary>
    /// Gets the strategy used to create detached state copies for management queries.
    /// </summary>
    public IEphemeralStateSnapshotter StateSnapshotter { get; init; } =
        SystemTextJsonEphemeralStateSnapshotter.Instance;

    /// <summary>
    /// Gets the step execution duration after which a step is marked apparently stuck.
    /// </summary>
    public TimeSpan? StuckStepThreshold { get; init; }

    /// <summary>
    /// Gets the optional maximum number of concurrent instance advancements in this process.
    /// </summary>
    public int? MaxConcurrentAdvancements { get; init; }

    /// <summary>
    /// Gets the optional maximum number of concurrently executing workflow steps in this process.
    /// </summary>
    public int? MaxConcurrentSteps { get; init; }

    /// <summary>
    /// Gets optional in-process named pool capacities keyed by pool name.
    /// </summary>
    public IDictionary<string, int> NamedPools { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    internal Action<InstanceId>? LaneWorkItemEnqueued { get; init; }

    internal Action? GovernanceWaitStarting { get; init; }
}
