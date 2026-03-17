namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record HistoryRecord(
    string TransitionType,
    DateTimeOffset Timestamp,
    string? Details);
