using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedEventEnvelope(
    string EventName,
    string CorrelationId,
    JsonElement? Payload,
    string? PayloadTypeKey,
    string EventId);
