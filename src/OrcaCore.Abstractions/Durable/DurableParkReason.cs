namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Categorizes why a durable instance requires explicit operator intervention.
/// </summary>
public enum DurableParkReason
{
    /// <summary>
    /// The persisted checkpoint payload does not carry a readable current execution envelope.
    /// </summary>
    RuntimeStateVersion,

    /// <summary>
    /// The definition version bound at start is not registered or is incompatible.
    /// </summary>
    VersionBinding,

    /// <summary>
    /// Advancement failed repeatedly and retrying automatically would loop hot.
    /// </summary>
    Poison
}
