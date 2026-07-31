using OrcaCore.Abstractions.Events;
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

    internal static string Create(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload switch
        {
            null => null,
            byte[] bytes => bytes,
            _ => throw new ArgumentException(
                "Durable event envelopes must carry a serialized byte-array payload.",
                nameof(envelope))
        };
        return Create(
            envelope.EventName,
            envelope.CorrelationId,
            envelope.OccurredAt,
            envelope.PayloadContentType,
            payload);
    }

    internal static string CreateExternalJobCompletion(string externalJobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalJobId);
        var normalized = CoreWorkflowValueCodec.Serialize(
            new NormalizedExternalJobCompletion(externalJobId),
            typeof(NormalizedExternalJobCompletion));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(normalized));
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

    private sealed record NormalizedExternalJobCompletion(string ExternalJobId);
}
