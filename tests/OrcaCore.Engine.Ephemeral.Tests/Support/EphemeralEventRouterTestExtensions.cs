namespace OrcaCore.Engine.Ephemeral.Tests.Support;

internal sealed record EphemeralTestEvent(
    EventId EventId,
    EventName EventName,
    CorrelationId CorrelationId,
    DateTimeOffset OccurredAt)
{
    internal static EphemeralTestEvent Create(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset? occurredAt = null) =>
        new(eventId, eventName, correlationId, occurredAt ?? DateTimeOffset.UtcNow);
}

internal sealed record EphemeralTestEvent<TPayload>(
    EventId EventId,
    EventName EventName,
    CorrelationId CorrelationId,
    TPayload Payload,
    DateTimeOffset OccurredAt)
{
    internal static EphemeralTestEvent<TPayload> Create(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        TPayload payload,
        DateTimeOffset? occurredAt = null) =>
        new(eventId, eventName, correlationId, payload, occurredAt ?? DateTimeOffset.UtcNow);
}

internal static class EphemeralEventRouterTestExtensions
{
    internal static ValueTask<EphemeralEventRouteResult> RouteToInstanceAsync(
        this EphemeralWorkflowEventRouter router,
        InstanceId instanceId,
        EphemeralTestEvent workflowEvent,
        CancellationToken cancellationToken = default) =>
        router.RouteAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(workflowEvent.EventName, EventContractVersion.Initial),
                workflowEvent.EventId,
                workflowEvent.CorrelationId,
                causationEventId: null,
                workflowEvent.OccurredAt,
                new WorkflowEventRoute.Direct(instanceId)),
            cancellationToken);

    internal static ValueTask<EphemeralEventRouteResult> RouteToInstanceAsync<TPayload>(
        this EphemeralWorkflowEventRouter router,
        InstanceId instanceId,
        EphemeralTestEvent<TPayload> workflowEvent,
        CancellationToken cancellationToken = default) =>
        router.RouteAsync(
            WorkflowInboundEvent<TPayload>.Create(
                WorkflowEventContract<TPayload>.Create(
                    workflowEvent.EventName,
                    EventContractVersion.Initial),
                workflowEvent.EventId,
                workflowEvent.CorrelationId,
                causationEventId: null,
                workflowEvent.OccurredAt,
                new WorkflowEventRoute.Direct(instanceId),
                workflowEvent.Payload),
            cancellationToken);
}
