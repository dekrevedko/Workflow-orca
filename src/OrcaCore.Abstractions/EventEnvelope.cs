namespace OrcaCore.Abstractions;

public sealed record EventEnvelope(
    string EventName,
    string CorrelationId,
    object? Payload,
    string EventId);
