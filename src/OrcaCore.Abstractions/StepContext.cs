namespace OrcaCore.Abstractions;

public sealed class StepContext<TState>(TState state, string instanceId, CancellationToken cancellationToken, EventEnvelope? resumedEvent = null)
{
    public TState State { get; } = state;
    public string InstanceId { get; } = instanceId;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public EventEnvelope? ResumedEvent { get; } = resumedEvent;
}
