namespace OrcaCore.Abstractions;

public sealed record WaitRecord(
    string WaitId,
    string EventName,
    string CorrelationId,
    string? BranchId,
    DateTimeOffset RegisteredAt,
    WaitStatus Status);
