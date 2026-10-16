namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record InboxRecord(
    string EventId,
    string InstanceId,
    PersistedEventEnvelope Envelope,
    DateTimeOffset ReceivedAt,
    bool Processed);
