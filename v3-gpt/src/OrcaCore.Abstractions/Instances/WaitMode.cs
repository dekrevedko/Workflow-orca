namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Describes whether an active wait keeps an instance hot or allows cold eviction.
/// </summary>
public enum WaitMode
{
    /// <summary>
    /// The wait is durable but may keep the current activation resident.
    /// </summary>
    Resident,

    /// <summary>
    /// The wait is durable and the instance is immediately evictable after commit.
    /// </summary>
    Cold
}
