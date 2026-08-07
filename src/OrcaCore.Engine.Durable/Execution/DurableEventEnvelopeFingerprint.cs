using OrcaCore.Abstractions.Durable;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableEventEnvelopeFingerprint
{
    internal static string Create(DurableEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return Create(
            envelope.EventName,
            envelope.EventContractVersion,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.CausationEventId,
            envelope.OccurredAt,
            envelope.Route,
            envelope.PayloadContentType,
            envelope.Payload);
    }

    internal static string Create(
        string eventName,
        int eventContractVersion,
        EventId? eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        DurableEventRouteEnvelope? route,
        string? payloadContentType,
        byte[]? payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        if (eventContractVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventContractVersion),
                eventContractVersion,
                "Event contract version must be positive.");
        }
        ArgumentNullException.ThrowIfNull(correlationId);

        var normalized = CoreWorkflowValueCodec.Serialize(
            new NormalizedEnvelope(
                eventName,
                eventContractVersion,
                eventId?.Value,
                correlationId.Value,
                causationEventId?.Value,
                occurredAt.ToUniversalTime(),
                route is null ? null : NormalizeRoute(route),
                payloadContentType,
                payload),
            typeof(NormalizedEnvelope));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(normalized));
    }

    private sealed record NormalizedEnvelope(
        string EventName,
        int EventContractVersion,
        string? EventId,
        string CorrelationId,
        string? CausationEventId,
        DateTimeOffset OccurredAt,
        NormalizedRoute? Route,
        string? PayloadContentType,
        byte[]? Payload);

    private static NormalizedRoute NormalizeRoute(DurableEventRouteEnvelope route)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(route.Kind);
        return new NormalizedRoute(
            route.Kind,
            route.InstanceId?.Value,
            route.DefinitionId?.Value,
            route.DefinitionVersion?.Value,
            route.StartIdempotencyKey,
            route.WorkflowInputContentType,
            route.WorkflowInputPayload);
    }

    private sealed record NormalizedRoute(
        string Kind,
        Guid? InstanceId,
        Guid? DefinitionId,
        int? DefinitionVersion,
        string? StartIdempotencyKey,
        string? WorkflowInputContentType,
        byte[]? WorkflowInputPayload);
}
