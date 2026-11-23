using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableInboundEventNormalizer
{
    private static readonly ConcurrentDictionary<Type, IStartOrDeliverRouteAdapter> StartOrDeliverAdapters = [];

    internal static NormalizedDurableInboundEvent Normalize(WorkflowInboundEvent inboundEvent)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        var bytes = inboundEvent.PayloadBytes;
        return bytes.IsEmpty
            ? Create(inboundEvent, payloadContentType: null, payload: null)
            : Create(inboundEvent, JsonWorkflowPayloadSerializer.JsonContentType, bytes.ToArray());
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
            Route = NormalizeRoute(inboundEvent),
            PayloadContentType = payloadContentType,
            Payload = payload
        };
        return new NormalizedDurableInboundEvent(
            envelope,
            DurableEventEnvelopeFingerprint.Create(envelope));
    }

    private static DurableEventRouteEnvelope NormalizeRoute(WorkflowInboundEvent inboundEvent)
    {
        return inboundEvent.Route switch
        {
            WorkflowEventRoute.Direct direct => new DurableEventRouteEnvelope
            {
                Kind = InboxRouteKinds.Direct,
                InstanceId = direct.InstanceId
            },
            WorkflowEventRoute.Correlation correlation => new DurableEventRouteEnvelope
            {
                Kind = InboxRouteKinds.Correlation,
                DefinitionId = correlation.DefinitionId
            },
            WorkflowEventRoute.DefinitionFanout fanout => new DurableEventRouteEnvelope
            {
                Kind = InboxRouteKinds.DefinitionFanout,
                DefinitionId = fanout.DefinitionId
            },
            _ => NormalizeStartOrDeliver(inboundEvent.Route, inboundEvent.StartInputBytes)
        };
    }

    private static DurableEventRouteEnvelope NormalizeStartOrDeliver(WorkflowEventRoute route, ReadOnlyMemory<byte> inputBytes)
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
        return adapter.Normalize(route, inputBytes);
    }

    private interface IStartOrDeliverRouteAdapter
    {
        DurableEventRouteEnvelope Normalize(WorkflowEventRoute route, ReadOnlyMemory<byte> inputBytes);
    }

    private sealed class StartOrDeliverRouteAdapter<TInput> : IStartOrDeliverRouteAdapter
    {
        public DurableEventRouteEnvelope Normalize(WorkflowEventRoute route, ReadOnlyMemory<byte> inputBytes)
        {
            var startOrDeliver = (WorkflowEventRoute.StartOrDeliver<TInput>)route;
            return new DurableEventRouteEnvelope
            {
                Kind = InboxRouteKinds.StartOrDeliver,
                DefinitionId = startOrDeliver.DefinitionId,
                DefinitionVersion = startOrDeliver.DefinitionVersion,
                StartIdempotencyKey = startOrDeliver.StartIdempotencyKey.Value,
                WorkflowInputContentType = JsonWorkflowPayloadSerializer.JsonContentType,
                WorkflowInputPayload = inputBytes.ToArray()
            };
        }
    }
}

internal sealed record NormalizedDurableInboundEvent(
    DurableEventEnvelope Envelope,
    string Fingerprint);
