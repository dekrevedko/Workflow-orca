using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Runtime-owned wait record (EV-021). Instance-targeted only in this task: branch identity
/// and timeout are out of scope (T1-10+) and simply absent. Internal — never exposed publicly
/// (CR-021); callers observe waits only through snapshot-shaped inspection surfaces.
/// </summary>
internal sealed class ActiveWait(WaitId waitId, string eventName, CorrelationId correlationId, DateTimeOffset registeredAt)
{
    public WaitId WaitId { get; } = waitId;

    public string EventName { get; } = eventName;

    public CorrelationId CorrelationId { get; } = correlationId;

    public DateTimeOffset RegisteredAt { get; } = registeredAt;

    public WaitStatus Status { get; set; } = WaitStatus.Active;
}
