namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Describes the runtime residency selected for a registered durable wait.
/// </summary>
/// <remarks>
/// This is an engine/provider protocol value, not an application authoring option. A durable
/// host may rehydrate either residency without changing the authored workflow contract.
/// </remarks>
public enum WaitMode
{
    /// <summary>
    /// The current runtime activation may remain resident after the wait is committed.
    /// </summary>
    Resident,

    /// <summary>
    /// The current runtime activation may be evicted after the wait is committed.
    /// </summary>
    Cold
}
