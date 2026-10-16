namespace OrcaCore.EventDrivenPrototype.Persistence;

public sealed record PrototypeStreamRecord(
    int Version,
    string EventType,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, object?> Data);
