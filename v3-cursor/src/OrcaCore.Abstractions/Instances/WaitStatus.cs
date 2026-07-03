namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Lifecycle status of a runtime-owned wait record (EV-021).
/// </summary>
public enum WaitStatus
{
    /// <summary>The wait is registered and eligible to be matched by an inbound event.</summary>
    Active = 0,

    /// <summary>The wait was matched by an event and consumed exactly once (EV-023).</summary>
    Matched = 1,

    /// <summary>The wait was cancelled before it matched (EV-044).</summary>
    Cancelled = 2,
}
