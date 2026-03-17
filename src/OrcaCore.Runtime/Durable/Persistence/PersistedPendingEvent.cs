namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedPendingEvent(
    PersistedEventEnvelope Envelope,
    DateTimeOffset ReceivedAt,
    bool Consumed);
