using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Metadata-only immutable view of a runtime-owned wait record (EV-021), nested under
/// <see cref="WorkflowInstanceSnapshot.ActiveWaits"/>. Never a live runtime object (CR-021).
/// </summary>
/// <param name="WaitId">Identity of this wait record.</param>
/// <param name="EventName">Logical event name this wait matches on (EV-020).</param>
/// <param name="CorrelationId">Request-reply identity paired with this wait (EV-002).</param>
/// <param name="RegisteredAt">UTC timestamp when the wait was registered.</param>
/// <param name="Status">Current wait status.</param>
/// <param name="Mode">Residency mode of this wait.</param>
public sealed record ActiveWaitSnapshot(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    WaitStatus Status,
    WaitMode Mode);
