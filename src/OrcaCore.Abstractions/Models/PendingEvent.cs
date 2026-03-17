namespace OrcaCore.Abstractions.Models;

public sealed record PendingEvent(
    EventEnvelope Envelope,
    DateTimeOffset ReceivedAt,
    bool Consumed);
