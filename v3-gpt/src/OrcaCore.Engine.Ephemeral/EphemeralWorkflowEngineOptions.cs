using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Configures in-process operational behavior for the ephemeral workflow engine.
/// </summary>
public sealed record EphemeralWorkflowEngineOptions
{
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
