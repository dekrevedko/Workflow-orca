using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides an immutable metadata-only view of one product lifecycle event.
/// </summary>
public sealed record LifecycleEventSnapshot
{
    /// <summary>
    /// Gets the workflow instance identity that emitted the lifecycle event.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the lifecycle event contract name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the step path when the lifecycle event belongs to a step.
    /// </summary>
    public string? StepPath { get; init; }

    /// <summary>
    /// Gets the workflow status associated with the lifecycle event when applicable.
    /// </summary>
    public global::OrcaCore.WorkflowInstanceStatus? Status { get; init; }

    /// <summary>
    /// Gets when the lifecycle event occurred.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets whether the lifecycle event is committed durably and survives restart.
    /// </summary>
    public required bool Durable { get; init; }
}
