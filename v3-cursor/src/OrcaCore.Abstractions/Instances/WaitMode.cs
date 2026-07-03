namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Residency mode of a runtime-owned wait record (EV-021).
/// </summary>
public enum WaitMode
{
    /// <summary>
    /// The wait keeps the instance hot in the current activation (EV-040). Ephemeral mode
    /// only ever produces resident waits.
    /// </summary>
    Resident = 0,

    /// <summary>
    /// Durable-only cold wait (EV-041): after registration commits the instance is
    /// immediately evictable. Unreachable from the ephemeral engine.
    /// </summary>
    Cold = 1,
}
