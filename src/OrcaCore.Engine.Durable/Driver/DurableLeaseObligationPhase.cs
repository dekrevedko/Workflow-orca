namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Persistent lifecycle of one scoped logical-capacity obligation.
/// </summary>
internal enum DurableLeaseObligationPhase
{
    Queued,
    PendingCommit,
    Held,
    ReviewMarked,
    AmbiguousHeld,
    Quarantined,
    Released,
    CancelledBeforeGrant,
    LeaseLost
}
