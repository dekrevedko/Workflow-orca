using System.Reflection;

namespace OrcaCore.TestSupport;

public enum ProcessLocalEventRouteStatus
{
    Accepted,
    Duplicate,
    NoActiveWait,
    InstanceTerminal,
    EventConflict,
    InstanceNotFound
}

public sealed record ProcessLocalEventRouteResult(
    ProcessLocalEventRouteStatus Status,
    InstanceId? InstanceId);

public sealed record ProcessLocalInboundEvent(
    EventId EventId,
    EventName EventName,
    CorrelationId CorrelationId,
    DateTimeOffset OccurredAt)
{
    public static ProcessLocalInboundEvent Create(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset? occurredAt = null) =>
        new(eventId, eventName, correlationId, occurredAt ?? DateTimeOffset.UtcNow);
}

public sealed record ProcessLocalInboundEvent<TPayload>
{
    private ProcessLocalInboundEvent(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        WorkflowInboundEvent<TPayload> detached)
    {
        EventId = eventId;
        EventName = eventName;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
        Payload = detached.Payload;
    }

    public EventId EventId { get; }

    public EventName EventName { get; }

    public CorrelationId CorrelationId { get; }

    public TPayload Payload { get; }

    public DateTimeOffset OccurredAt { get; }

    public static ProcessLocalInboundEvent<TPayload> Create(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        TPayload payload,
        DateTimeOffset? occurredAt = null)
    {
        var timestamp = (occurredAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var detached = WorkflowInboundEvent<TPayload>.Create(
            WorkflowEventContract<TPayload>.Create(eventName, EventContractVersion.Initial),
            eventId,
            correlationId,
            causationEventId: null,
            timestamp,
            new WorkflowEventRoute.Direct(InstanceId.Parse(Guid.CreateVersion7().ToString())),
            payload);
        return new ProcessLocalInboundEvent<TPayload>(
            eventId,
            eventName,
            correlationId,
            timestamp,
            detached);
    }
}

/// <summary>
/// Keeps process-local engine behavior scenarios off the removed public event-client surface.
/// Public durable-ingress contract tests use <c>IWorkflowEventIngress</c> directly.
/// </summary>
public sealed class ProcessLocalEventRouter(IServiceProvider services)
{
    public ValueTask<ProcessLocalEventRouteResult> RouteAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            inboundEvent,
            (inboundEvent.Route as WorkflowEventRoute.Direct)?.InstanceId,
            cancellationToken);

    public ValueTask<ProcessLocalEventRouteResult> RouteAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            inboundEvent,
            (inboundEvent.Route as WorkflowEventRoute.Direct)?.InstanceId,
            cancellationToken);

    public ValueTask<ProcessLocalEventRouteResult> RouteToInstanceAsync(
        InstanceId instanceId,
        ProcessLocalInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(inboundEvent.EventName, EventContractVersion.Initial),
                inboundEvent.EventId,
                inboundEvent.CorrelationId,
                causationEventId: null,
                inboundEvent.OccurredAt,
                new WorkflowEventRoute.Direct(instanceId)),
            instanceId,
            cancellationToken);

    public ValueTask<ProcessLocalEventRouteResult> RouteToInstanceAsync<TPayload>(
        InstanceId instanceId,
        ProcessLocalInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            WorkflowInboundEvent<TPayload>.Create(
                WorkflowEventContract<TPayload>.Create(
                    inboundEvent.EventName,
                    EventContractVersion.Initial),
                inboundEvent.EventId,
                inboundEvent.CorrelationId,
                causationEventId: null,
                inboundEvent.OccurredAt,
                new WorkflowEventRoute.Direct(instanceId),
                inboundEvent.Payload),
            instanceId,
            cancellationToken);

    public ValueTask<ProcessLocalEventRouteResult> RouteByCorrelationAsync(
        DefinitionId definitionId,
        ProcessLocalInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            WorkflowInboundEvent.Create(
                WorkflowEventContract.Create(inboundEvent.EventName, EventContractVersion.Initial),
                inboundEvent.EventId,
                inboundEvent.CorrelationId,
                causationEventId: null,
                inboundEvent.OccurredAt,
                new WorkflowEventRoute.Correlation(definitionId)),
            directInstanceId: null,
            cancellationToken);

    public ValueTask<ProcessLocalEventRouteResult> RouteByCorrelationAsync<TPayload>(
        DefinitionId definitionId,
        ProcessLocalInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        RouteCoreAsync(
            WorkflowInboundEvent<TPayload>.Create(
                WorkflowEventContract<TPayload>.Create(
                    inboundEvent.EventName,
                    EventContractVersion.Initial),
                inboundEvent.EventId,
                inboundEvent.CorrelationId,
                causationEventId: null,
                inboundEvent.OccurredAt,
                new WorkflowEventRoute.Correlation(definitionId),
                inboundEvent.Payload),
            directInstanceId: null,
            cancellationToken);

    private async ValueTask<ProcessLocalEventRouteResult> RouteCoreAsync(
        WorkflowInboundEvent inboundEvent,
        InstanceId? directInstanceId,
        CancellationToken cancellationToken)
    {
        var durableIngressType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("OrcaCore.Durable.Hosting.IWorkflowEventIngress"))
            .FirstOrDefault(type => type is not null);
        if (durableIngressType is not null && services.GetService(durableIngressType) is { } durableIngress)
        {
            return await InvokeAsync(
                durableIngress,
                "AcceptAsync",
                inboundEvent,
                directInstanceId,
                cancellationToken).ConfigureAwait(false);
        }

        var routerType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("OrcaCore.Engine.Ephemeral.EphemeralWorkflowEventRouter"))
            .FirstOrDefault(type => type is not null)
            ?? throw new InvalidOperationException("The process-local event router is not loaded.");
        var router = services.GetService(routerType)
            ?? throw new InvalidOperationException("The process-local event router is not registered.");
        return await InvokeAsync(
            router,
            "RouteAsync",
            inboundEvent,
            directInstanceId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ProcessLocalEventRouteResult> InvokeAsync(
        object target,
        string methodName,
        WorkflowInboundEvent inboundEvent,
        InstanceId? directInstanceId,
        CancellationToken cancellationToken)
    {
        var payloadType = FindPayloadType(inboundEvent.GetType());
        var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate =>
                candidate.Name == methodName &&
                candidate.IsGenericMethodDefinition == (payloadType is not null));
        if (payloadType is not null)
        {
            method = method.MakeGenericMethod(payloadType);
        }

        var valueTask = method.Invoke(target, [inboundEvent, cancellationToken])!;
        var task = (Task)valueTask.GetType().GetMethod(nameof(ValueTask<object>.AsTask))!.Invoke(valueTask, null)!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task)!;
        return MapResult(result, directInstanceId);
    }

    private static Type? FindPayloadType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(WorkflowInboundEvent<>)
            ? type.GetGenericArguments()[0]
            : null;

    private static ProcessLocalEventRouteResult MapResult(object result, InstanceId? directInstanceId)
    {
        var resultType = result.GetType();
        var name = resultType.Name;
        if (name == "Accepted")
        {
            return new ProcessLocalEventRouteResult(ProcessLocalEventRouteStatus.Accepted, directInstanceId);
        }

        if (name == "Duplicate")
        {
            return new ProcessLocalEventRouteResult(ProcessLocalEventRouteStatus.Duplicate, directInstanceId);
        }

        if (name == "Rejected")
        {
            var reason = resultType.GetProperty("Reason")!.GetValue(result)!;
            var rejectionStatus = reason.GetType().Name switch
            {
                "EventConflict" => ProcessLocalEventRouteStatus.EventConflict,
                "DirectInstanceTerminal" => ProcessLocalEventRouteStatus.InstanceTerminal,
                "DirectInstanceNotFound" => ProcessLocalEventRouteStatus.InstanceNotFound,
                _ => throw new InvalidOperationException(
                    $"The durable ingress rejected the event with '{reason.GetType().Name}'.")
            };
            return new ProcessLocalEventRouteResult(rejectionStatus, directInstanceId);
        }

        var statusName = resultType.GetProperty("Status")!.GetValue(result)!.ToString()!;
        var status = Enum.Parse<ProcessLocalEventRouteStatus>(statusName, ignoreCase: false);
        var instanceId = (InstanceId?)resultType.GetProperty("InstanceId")!.GetValue(result);
        return new ProcessLocalEventRouteResult(status, instanceId);
    }
}
