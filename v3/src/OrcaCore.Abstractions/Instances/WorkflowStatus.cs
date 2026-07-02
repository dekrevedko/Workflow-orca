namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// The shared instance lifecycle status (CR-030). One enum serves both engines; transitions
/// are governed by an explicit table with named triggers.
/// </summary>
public enum WorkflowStatus
{
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled,
    Terminated,

    /// <summary>
    /// Durable-only status (CR-030). This value is inert shared contract: the ephemeral
    /// engine never produces it, and no ephemeral API accepts it — it exists here only so
    /// the status type stays shared across both engines.
    /// </summary>
    Paused,
}
