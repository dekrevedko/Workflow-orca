namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Describes the lifecycle status of a workflow instance.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// The instance is actively executing or ready to execute.
    /// </summary>
    Running,

    /// <summary>
    /// The instance is suspended on runtime-owned work such as an event wait.
    /// </summary>
    Waiting,

    /// <summary>
    /// The instance reached a successful terminal state.
    /// </summary>
    Completed,

    /// <summary>
    /// The instance reached a failed terminal state.
    /// </summary>
    Failed,

    /// <summary>
    /// The instance reached a cooperative cancellation terminal state.
    /// </summary>
    Cancelled,

    /// <summary>
    /// The instance reached a forced termination terminal state.
    /// </summary>
    Terminated,

    /// <summary>
    /// The saga reached a terminal state after successful compensation.
    /// </summary>
    Compensated,

    /// <summary>
    /// The saga reached a terminal state because a compensating action failed.
    /// </summary>
    CompensationFailed,

    /// <summary>
    /// Durable-only status. The ephemeral engine never produces Paused and no ephemeral API accepts it.
    /// </summary>
    Paused
}
