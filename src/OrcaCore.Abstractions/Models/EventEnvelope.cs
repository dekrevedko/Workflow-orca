namespace OrcaCore.Abstractions.Models;

public sealed record EventEnvelope(
    string EventName,
    string CorrelationId,
    object? Payload,
    string EventId,
    Type? DeclaredPayloadType = null)
{
    public Type? PayloadType => DeclaredPayloadType ?? Payload?.GetType();
}
