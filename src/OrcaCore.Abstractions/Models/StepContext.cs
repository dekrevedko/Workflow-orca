namespace OrcaCore.Abstractions.Models;

public sealed class StepContext<TState>(
    TState state,
    string instanceId,
    string definitionId,
    string currentNodePath,
    CancellationToken cancellationToken,
    EventEnvelope? resumedEvent = null)
{
    public TState State { get; } = state;
    public string InstanceId { get; } = instanceId;
    public string DefinitionId { get; } = definitionId;
    public string CurrentNodePath { get; } = currentNodePath;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public EventEnvelope? ResumedEvent { get; } = resumedEvent;
    public bool IsResumed => ResumedEvent is not null;
}
