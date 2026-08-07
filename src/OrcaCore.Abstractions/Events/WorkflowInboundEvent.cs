using OrcaCore.Internal;

namespace OrcaCore;

/// <summary>Specifies the one durable routing intent carried by an inbound workflow event.</summary>
public abstract record WorkflowEventRoute
{
    private protected WorkflowEventRoute()
    {
    }

    /// <summary>Routes to one exact workflow instance.</summary>
    public sealed record Direct(InstanceId InstanceId) : WorkflowEventRoute;

    /// <summary>Routes by the unique active definition/contract/correlation wait.</summary>
    public sealed record Correlation(DefinitionId DefinitionId) : WorkflowEventRoute;

    /// <summary>Routes to a committed snapshot of current nonterminal definition instances.</summary>
    public sealed record DefinitionFanout(DefinitionId DefinitionId) : WorkflowEventRoute;

    /// <summary>Routes to, or starts, one exact durable workflow definition.</summary>
    public sealed record StartOrDeliver<TInput>(
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        StartIdempotencyKey StartIdempotencyKey,
        TInput WorkflowInput) : WorkflowEventRoute, IWorkflowEventRouteNormalizer
    {
        void IWorkflowEventRouteNormalizer.Validate()
        {
            ArgumentNullException.ThrowIfNull(DefinitionId);
            ArgumentNullException.ThrowIfNull(DefinitionVersion);
            ArgumentNullException.ThrowIfNull(StartIdempotencyKey);
            _ = FixedWorkflowValueCodec.Serialize(WorkflowInput, typeof(TInput));
        }

        WorkflowEventRoute IWorkflowEventRouteNormalizer.Normalize()
        {
            var bytes = FixedWorkflowValueCodec.Serialize(WorkflowInput, typeof(TInput));
            var detached = (TInput?)FixedWorkflowValueCodec.Deserialize(bytes, typeof(TInput));
            return this with { WorkflowInput = detached! };
        }
    }
}

/// <summary>One immutable payloadless event submitted to durable ingress.</summary>
public class WorkflowInboundEvent
{
    private protected WorkflowInboundEvent(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route)
    {
        EventContract = eventContract;
        EventId = eventId;
        CorrelationId = correlationId;
        CausationEventId = causationEventId;
        OccurredAt = occurredAt;
        Route = route;
    }

    /// <summary>Gets the exact name and version accepted by a matching wait.</summary>
    public WorkflowEventContract EventContract { get; }

    /// <summary>Gets the globally unique inbound event identity.</summary>
    public EventId EventId { get; }

    /// <summary>Gets the application correlation identity.</summary>
    public CorrelationId CorrelationId { get; }

    /// <summary>Gets the optional inbound event that caused this event.</summary>
    public EventId? CausationEventId { get; }

    /// <summary>Gets the normalized UTC occurrence time.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets the single closed routing intent.</summary>
    public WorkflowEventRoute Route { get; }

    /// <summary>Creates an immutable payloadless inbound event.</summary>
    public static WorkflowInboundEvent Create(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route)
    {
        Validate(eventContract, eventId, correlationId, causationEventId, occurredAt, route);
        if (eventContract.BoundPayloadType is not null)
        {
            throw new ArgumentException(
                "A typed event contract requires a typed inbound event.",
                nameof(eventContract));
        }

        return new WorkflowInboundEvent(
            eventContract,
            eventId,
            correlationId,
            causationEventId,
            occurredAt.ToUniversalTime(),
            NormalizeRoute(route));
    }

    internal static void Validate(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(route);
        if (occurredAt == default)
        {
            throw new ArgumentException("Occurrence time must be non-default.", nameof(occurredAt));
        }

        if (causationEventId is not null && causationEventId.Equals(eventId))
        {
            throw new ArgumentException(
                "An event cannot identify itself as its causation event.",
                nameof(causationEventId));
        }

        ValidateRoute(route);
    }

    internal static WorkflowEventRoute NormalizeRoute(WorkflowEventRoute route)
    {
        return route is IWorkflowEventRouteNormalizer normalizer ? normalizer.Normalize() : route;
    }

    private static void ValidateRoute(WorkflowEventRoute route)
    {
        switch (route)
        {
            case WorkflowEventRoute.Direct direct:
                ArgumentNullException.ThrowIfNull(direct.InstanceId);
                break;
            case WorkflowEventRoute.Correlation correlation:
                ArgumentNullException.ThrowIfNull(correlation.DefinitionId);
                break;
            case WorkflowEventRoute.DefinitionFanout fanout:
                ArgumentNullException.ThrowIfNull(fanout.DefinitionId);
                break;
            case IWorkflowEventRouteNormalizer normalizer:
                normalizer.Validate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, "The event route is not supported.");
        }
    }
}

/// <summary>One immutable typed event submitted to durable ingress.</summary>
public sealed class WorkflowInboundEvent<TPayload> : WorkflowInboundEvent
{
    private readonly byte[] payloadBytes;

    private WorkflowInboundEvent(
        WorkflowEventContract<TPayload> eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route,
        TPayload payload,
        byte[] payloadBytes)
        : base(eventContract, eventId, correlationId, causationEventId, occurredAt, route)
    {
        EventContract = eventContract;
        Payload = payload;
        this.payloadBytes = payloadBytes;
    }

    /// <summary>Gets the exact typed event contract.</summary>
    public new WorkflowEventContract<TPayload> EventContract { get; }

    /// <summary>Gets the detached event payload.</summary>
    public TPayload Payload { get; }

    /// <summary>Creates an immutable typed inbound event.</summary>
    public static WorkflowInboundEvent<TPayload> Create(
        WorkflowEventContract<TPayload> eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        WorkflowEventRoute route,
        TPayload payload)
    {
        Validate(eventContract, eventId, correlationId, causationEventId, occurredAt, route);
        var bytes = FixedWorkflowValueCodec.Serialize(payload, typeof(TPayload));
        var detached = (TPayload?)FixedWorkflowValueCodec.Deserialize(bytes, typeof(TPayload));
        return new WorkflowInboundEvent<TPayload>(
            eventContract,
            eventId,
            correlationId,
            causationEventId,
            occurredAt.ToUniversalTime(),
            NormalizeRoute(route),
            detached!,
            bytes);
    }

    internal ReadOnlyMemory<byte> PayloadBytes => payloadBytes;
}

/// <summary>Describes why durable ingress could not acquire ownership of an event.</summary>
public abstract record WorkflowEventAcceptanceRejection
{
    private protected WorkflowEventAcceptanceRejection()
    {
    }

    public sealed record EventConflict : WorkflowEventAcceptanceRejection;

    public sealed record DirectInstanceNotFound : WorkflowEventAcceptanceRejection;

    public sealed record DirectInstanceTerminal : WorkflowEventAcceptanceRejection;

    public sealed record StartConflict(StartIdempotencyConflict Conflict) : WorkflowEventAcceptanceRejection;

    public sealed record FanoutLimitExceeded : WorkflowEventAcceptanceRejection;
}

/// <summary>Describes the acknowledgement-safe outcome of durable event ingress.</summary>
public abstract record WorkflowEventAcceptanceResult
{
    private protected WorkflowEventAcceptanceResult()
    {
    }

    public sealed record Accepted : WorkflowEventAcceptanceResult;

    public sealed record Duplicate : WorkflowEventAcceptanceResult;

    public sealed record Rejected(WorkflowEventAcceptanceRejection Reason) : WorkflowEventAcceptanceResult;
}

internal interface IWorkflowEventRouteNormalizer
{
    void Validate();

    WorkflowEventRoute Normalize();
}
