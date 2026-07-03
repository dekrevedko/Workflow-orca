namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Shared lifecycle status for workflow instances in both execution modes (CR-030).
/// </summary>
public enum WorkflowStatus
{
    /// <summary>The instance is actively executing or scheduled to continue.</summary>
    Running = 0,

    /// <summary>The instance is suspended on one or more registered event waits.</summary>
    Waiting = 1,

    /// <summary>The instance reached a successful terminal outcome.</summary>
    Completed = 2,

    /// <summary>The instance reached a failure terminal outcome.</summary>
    Failed = 3,

    /// <summary>The instance was cooperatively cancelled.</summary>
    Cancelled = 4,

    /// <summary>The instance was forcibly terminated by an operator action.</summary>
    Terminated = 5,

    /// <summary>
    /// Durable-only paused state. Unreachable in ephemeral mode: the ephemeral engine never
    /// produces this value, and no ephemeral API accepts it.
    /// </summary>
    Paused = 6,
}
