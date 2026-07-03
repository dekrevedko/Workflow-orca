using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Events;

/// <summary>
/// Normalized inbound event shape consumed by workflow instances (EV-001).
/// </summary>
/// <param name="EventId">Deduplication identity for this delivery.</param>
/// <param name="EventName">Logical event name used for wait matching.</param>
/// <param name="CorrelationId">Request-reply identity paired with the originating wait.</param>
/// <param name="Payload">Optional application payload; may be null when no data is supplied.</param>
/// <param name="OccurredAt">Timestamp when the event occurred in the external system.</param>
public sealed record EventEnvelope(
    EventId EventId,
    string EventName,
    CorrelationId CorrelationId,
    object? Payload,
    DateTimeOffset OccurredAt);
