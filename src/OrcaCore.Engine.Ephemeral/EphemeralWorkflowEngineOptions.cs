using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Configures in-process operational behavior for the ephemeral workflow engine.
/// </summary>
internal sealed record EphemeralWorkflowEngineOptions
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
    /// Gets the step execution duration after which a step is marked apparently stuck.
    /// </summary>
    public TimeSpan? StuckStepThreshold { get; init; }

    internal Action<InstanceId>? LaneWorkItemEnqueued { get; init; }

    internal Action? GovernanceWaitStarting { get; init; }

    internal int MaxConcurrentExecutionPathsPerInstance { get; init; } = int.MaxValue;

    internal IReadOnlyDictionary<Type, int> StepThrottles { get; init; } =
        new Dictionary<Type, int>();

    internal IReadOnlyDictionary<string, int> TransientPools { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    internal static EphemeralWorkflowEngineOptions FromHostOptions(
        global::OrcaCore.Hosting.EphemeralEngineHostOptions hostOptions)
    {
        ArgumentNullException.ThrowIfNull(hostOptions);
        var validated = hostOptions.ValidateAndCopy();
        return new EphemeralWorkflowEngineOptions
        {
            MaxConcurrentExecutionPathsPerInstance =
                validated.MaxConcurrentExecutionPathsPerInstance,
            StepThrottles = validated.StepThrottles,
            TransientPools = validated.TransientPools
        };
    }
}
