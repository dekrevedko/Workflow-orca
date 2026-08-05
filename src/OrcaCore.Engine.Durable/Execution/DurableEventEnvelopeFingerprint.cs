using OrcaCore.Abstractions.Durable;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableEventEnvelopeFingerprint
{
    internal static string Create<TPayload>(
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        TPayload payload)
    {
        var serializedPayload = payload is null
            ? null
            : CoreWorkflowValueCodec.Serialize(payload, typeof(TPayload));
        return Create(
            eventName.Value,
            correlationId,
            occurredAt,
            serializedPayload is null ? null : JsonWorkflowPayloadSerializer.JsonContentType,
            serializedPayload);
    }

    internal static string Create(DurableEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Create(
            envelope.EventName,
            envelope.CorrelationId,
            envelope.OccurredAt,
            envelope.PayloadContentType,
            envelope.Payload);
    }

    internal static string Create(
        string eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        string? payloadContentType,
        byte[]? payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationId);

        var normalized = CoreWorkflowValueCodec.Serialize(
            new NormalizedEnvelope(
                eventName,
                correlationId.Value,
                occurredAt.ToUniversalTime(),
                payloadContentType,
                payload),
            typeof(NormalizedEnvelope));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(normalized));
    }

    private sealed record NormalizedEnvelope(
        string EventName,
        string CorrelationId,
        DateTimeOffset OccurredAt,
        string? PayloadContentType,
        byte[]? Payload);
}
