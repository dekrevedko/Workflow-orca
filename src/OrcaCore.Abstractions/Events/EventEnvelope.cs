using OrcaCore.Internal;

namespace OrcaCore;

/// <summary>
/// Provides the fixed-codec event value that resumed a workflow step.
/// </summary>
public sealed class EventEnvelope
{
    private readonly byte[] payload;

    internal EventEnvelope(
        EventId eventId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload)
    {
        EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
        EventContract = eventContract ?? throw new ArgumentNullException(nameof(eventContract));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        if (occurredAt == default)
        {
            throw new ArgumentException("Occurrence time must be non-default.", nameof(occurredAt));
        }

        OccurredAt = occurredAt.ToUniversalTime();
        this.payload = payload.ToArray();
    }

    /// <summary>Gets the event identity used for deduplication.</summary>
    public EventId EventId { get; }

    /// <summary>Gets the event descriptor matched against the active wait.</summary>
    public WorkflowEventContract EventContract { get; }

    /// <summary>Gets the request-reply correlation identity.</summary>
    public CorrelationId CorrelationId { get; }

    /// <summary>Gets when the event occurred.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Materializes a detached payload through the fixed workflow-value codec.</summary>
    public TPayload GetPayload<TPayload>(WorkflowEventContract<TPayload> eventContract)
    {
        ArgumentNullException.ThrowIfNull(eventContract);
        if (!EventContract.Equals(eventContract) ||
            (EventContract.BoundPayloadType is { } boundPayloadType &&
             boundPayloadType != eventContract.BoundPayloadType))
        {
            throw new ArgumentException(
                "The requested payload descriptor does not match the resumed event name, version, and bound payload type.",
                nameof(eventContract));
        }

        if (payload.Length == 0)
        {
            throw new InvalidOperationException("The resumed event does not contain a payload.");
        }

        return (TPayload?)FixedWorkflowValueCodec.Deserialize(payload, typeof(TPayload)) ??
            throw new InvalidOperationException(
                $"The resumed event payload materialized as null for '{typeof(TPayload).FullName}'.");
    }
}
