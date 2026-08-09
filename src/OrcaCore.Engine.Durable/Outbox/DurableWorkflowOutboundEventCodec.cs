using System.Text.Json;

namespace OrcaCore.Engine.Durable.Outbox;

internal sealed record DurableWorkflowOutboundEventData(
    string EventName,
    int EventContractVersion,
    string? PayloadTypeName,
    string EventId,
    string CorrelationId,
    string? CausationEventId,
    DateTimeOffset OccurredAt,
    string OriginInstanceId,
    string OriginDefinitionId,
    int OriginDefinitionVersion,
    byte[] Payload);

internal static class DurableWorkflowOutboundEventCodec
{
    internal static byte[] Encode(DurableWorkflowOutboundEventData outboundEvent)
    {
        ArgumentNullException.ThrowIfNull(outboundEvent);
        return JsonSerializer.SerializeToUtf8Bytes(outboundEvent);
    }

    internal static DurableWorkflowOutboundEventData Decode(ReadOnlySpan<byte> payload)
    {
        var outboundEvent = JsonSerializer.Deserialize<DurableWorkflowOutboundEventData>(payload) ??
            throw new InvalidOperationException("The workflow-event outbox payload is empty.");
        ArgumentException.ThrowIfNullOrWhiteSpace(outboundEvent.EventName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outboundEvent.EventContractVersion);
        if (outboundEvent.PayloadTypeName is { } payloadTypeName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(payloadTypeName);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outboundEvent.EventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboundEvent.CorrelationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboundEvent.OriginInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outboundEvent.OriginDefinitionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(outboundEvent.OriginDefinitionVersion);
        ArgumentNullException.ThrowIfNull(outboundEvent.Payload);
        if (outboundEvent.OccurredAt == default)
        {
            throw new InvalidOperationException("The workflow-event outbox occurrence time is missing.");
        }

        return outboundEvent;
    }
}
