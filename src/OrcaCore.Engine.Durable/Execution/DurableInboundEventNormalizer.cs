using System.Collections.Concurrent;
using System.Security.Cryptography;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableInboundEventNormalizer
{
    private static readonly ConcurrentDictionary<Type, IStartOrDeliverRouteAdapter> StartOrDeliverAdapters = [];

    internal static NormalizedDurableInboundEvent Normalize(WorkflowInboundEvent inboundEvent)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        return Create(inboundEvent, payloadContentType: null, payload: null);
    }

    internal static NormalizedDurableInboundEvent Normalize<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        var payload = CoreWorkflowValueCodec.Serialize(inboundEvent.Payload, typeof(TPayload));
        return Create(inboundEvent, JsonWorkflowPayloadSerializer.JsonContentType, payload);
    }

    private static NormalizedDurableInboundEvent Create(
        WorkflowInboundEvent inboundEvent,
        string? payloadContentType,
        byte[]? payload)
    {
        var envelope = new DurableEventEnvelope
        {
            EventId = inboundEvent.EventId,
            EventName = inboundEvent.EventContract.EventName.Value,
            EventContractVersion = inboundEvent.EventContract.Version.Value,
            CorrelationId = inboundEvent.CorrelationId,
            CausationEventId = inboundEvent.CausationEventId,
            OccurredAt = inboundEvent.OccurredAt.ToUniversalTime(),
            Route = NormalizeRoute(inboundEvent.Route),
            PayloadContentType = payloadContentType,
            Payload = payload
        };
        return new NormalizedDurableInboundEvent(
            envelope,
            DurableEventEnvelopeFingerprint.Create(envelope));
    }

    private static DurableEventRouteEnvelope NormalizeRoute(WorkflowEventRoute route)
    {
        return route switch
        {
            WorkflowEventRoute.Direct direct => new DurableEventRouteEnvelope
            {
                Kind = "direct",
                InstanceId = direct.InstanceId
            },
            WorkflowEventRoute.Correlation correlation => new DurableEventRouteEnvelope
            {
                Kind = "correlation",
                DefinitionId = correlation.DefinitionId
            },
            WorkflowEventRoute.DefinitionFanout fanout => new DurableEventRouteEnvelope
            {
                Kind = "definition-fanout",
                DefinitionId = fanout.DefinitionId
            },
            _ => NormalizeStartOrDeliver(route)
        };
    }

    private static DurableEventRouteEnvelope NormalizeStartOrDeliver(WorkflowEventRoute route)
    {
        var routeType = route.GetType();
        if (!routeType.IsGenericType ||
            routeType.GetGenericTypeDefinition() != typeof(WorkflowEventRoute.StartOrDeliver<>))
        {
            throw new ArgumentOutOfRangeException(nameof(route), route, "The event route is not supported.");
        }

        var inputType = routeType.GetGenericArguments()[0];
        var adapter = StartOrDeliverAdapters.GetOrAdd(inputType, static type =>
            (IStartOrDeliverRouteAdapter)Activator.CreateInstance(
                typeof(StartOrDeliverRouteAdapter<>).MakeGenericType(type))!);
        return adapter.Normalize(route);
    }

    private interface IStartOrDeliverRouteAdapter
    {
        DurableEventRouteEnvelope Normalize(WorkflowEventRoute route);
    }

    private sealed class StartOrDeliverRouteAdapter<TInput> : IStartOrDeliverRouteAdapter
    {
        public DurableEventRouteEnvelope Normalize(WorkflowEventRoute route)
        {
            var startOrDeliver = (WorkflowEventRoute.StartOrDeliver<TInput>)route;
            return new DurableEventRouteEnvelope
            {
                Kind = "start-or-deliver",
                DefinitionId = startOrDeliver.DefinitionId,
                DefinitionVersion = startOrDeliver.DefinitionVersion,
                StartIdempotencyKey = startOrDeliver.StartIdempotencyKey.Value,
                WorkflowInputContentType = JsonWorkflowPayloadSerializer.JsonContentType,
                WorkflowInputPayload = CoreWorkflowValueCodec.Serialize(
                    startOrDeliver.WorkflowInput,
                    typeof(TInput))
            };
        }
    }
}

internal sealed record NormalizedDurableInboundEvent(
    DurableEventEnvelope Envelope,
    string Fingerprint);

internal static class DurableWorkflowValueFingerprint
{
    internal static string Create(ReadOnlySpan<byte> payload) =>
        Convert.ToHexString(SHA256.HashData(payload));
}
