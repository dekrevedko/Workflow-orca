using OrcaCore.Internal;

namespace OrcaCore;

/// <summary>Provides one complete runtime-owned outbound workflow event.</summary>
public sealed class WorkflowOutboundEvent
{
    private readonly byte[] payload;

    internal WorkflowOutboundEvent(
        WorkflowEventContract eventContract,
        EventId eventId,
        CorrelationId correlationId,
        EventId? causationEventId,
        DateTimeOffset occurredAt,
        InstanceId originInstanceId,
        DefinitionId originDefinitionId,
        DefinitionVersion originDefinitionVersion,
        ReadOnlyMemory<byte> payload)
    {
        EventContract = eventContract ?? throw new ArgumentNullException(nameof(eventContract));
        EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        CausationEventId = causationEventId;
        if (occurredAt == default)
        {
            throw new ArgumentException("Occurrence time must be non-default.", nameof(occurredAt));
        }

        OccurredAt = occurredAt.ToUniversalTime();
        OriginInstanceId = originInstanceId ?? throw new ArgumentNullException(nameof(originInstanceId));
        OriginDefinitionId = originDefinitionId ?? throw new ArgumentNullException(nameof(originDefinitionId));
        OriginDefinitionVersion = originDefinitionVersion ??
            throw new ArgumentNullException(nameof(originDefinitionVersion));
        this.payload = payload.ToArray();
    }

    public WorkflowEventContract EventContract { get; }

    public EventId EventId { get; }

    public CorrelationId CorrelationId { get; }

    public EventId? CausationEventId { get; }

    public DateTimeOffset OccurredAt { get; }

    public InstanceId OriginInstanceId { get; }

    public DefinitionId OriginDefinitionId { get; }

    public DefinitionVersion OriginDefinitionVersion { get; }

    /// <summary>Materializes a detached payload through the fixed workflow-value codec.</summary>
    public TPayload GetPayload<TPayload>(WorkflowEventContract<TPayload> eventContract)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        if (!EventContract.Equals(eventContract) ||
            (EventContract.BoundPayloadType is { } boundPayloadType &&
             boundPayloadType != eventContract.BoundPayloadType))
        {
            throw new ArgumentException(
                "The requested payload descriptor does not match the outbound event name, version, and bound payload type.",
                nameof(eventContract));
        }

        if (payload.Length == 0)
        {
            throw new InvalidOperationException("The outbound event does not contain a payload.");
        }

        return (TPayload?)FixedWorkflowValueCodec.Deserialize(payload, typeof(TPayload)) ??
            throw new InvalidOperationException(
                $"The outbound event payload materialized as null for '{typeof(TPayload).FullName}'.");
    }
}
