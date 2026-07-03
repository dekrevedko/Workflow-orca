using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// Immutable, metadata-only view of one active wait (MG-030/CR-021, T1-13): the management
/// surface's <c>GetActiveWaits</c> result. Never a live <c>ActiveWait</c> reference.
/// </summary>
public sealed record ActiveWaitSnapshot(
    InstanceId InstanceId,
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt);
