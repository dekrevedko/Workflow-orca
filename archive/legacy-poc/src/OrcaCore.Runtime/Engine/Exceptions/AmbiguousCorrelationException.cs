namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class AmbiguousCorrelationException(string eventName, string correlationId, int matchCount)
    : WorkflowRoutingException(
        $"Ambiguous correlation - use instance-targeted or fanout routing: EventName='{eventName}', CorrelationId='{correlationId}' maps to {matchCount} instances.")
{
    public string EventName { get; } = eventName;

    public string CorrelationId { get; } = correlationId;

    public int MatchCount { get; } = matchCount;
}
