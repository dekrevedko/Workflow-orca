using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Events;

/// <summary>
/// The canonical normalized inbound event shape (EV-001). Provider/transport-specific
/// shapes are adapted to this envelope at the boundary.
/// </summary>
public sealed record EventEnvelope(
    EventId EventId,
    string EventName,
    CorrelationId CorrelationId,
    object? Payload,
    DateTimeOffset OccurredAt);
