namespace OrcaCore.EventDrivenPrototype.Persistence;

public sealed record PrototypeInboxRecord(
    string EventId,
    string EventName,
    string CorrelationId,
    DateTimeOffset ReceivedAt,
    bool Applied);
