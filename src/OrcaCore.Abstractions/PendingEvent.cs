namespace OrcaCore.Abstractions;

public sealed record PendingEvent(
    EventEnvelope Envelope,
    DateTimeOffset ReceivedAt,
    bool Consumed);
