using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedEventEnvelope(
    string EventName,
    string CorrelationId,
    SerializedPayloadEnvelope? PayloadEnvelope,
    string EventId)
{
    public PersistedEventEnvelope(
        string eventName,
        string correlationId,
        JsonElement? payload,
        string? payloadTypeKey,
        string eventId)
        : this(
            eventName,
            correlationId,
            payload is null || payloadTypeKey is null
                ? null
                : new SerializedPayloadEnvelope(
                    JsonPayloadEnvelopeSerializer.FromJsonElement(
                        payload.Value,
                        JsonPayloadEnvelopeSerializer.JsonContentType,
                        payloadTypeKey),
                    payloadTypeKey),
            eventId)
    {
    }

    public JsonElement? Payload =>
        PayloadEnvelope is null
            ? null
            : JsonPayloadEnvelopeSerializer.ToJsonElement(PayloadEnvelope.Payload);

    public string? PayloadTypeKey => PayloadEnvelope?.TypeKey;
}
