namespace OrcaCore.Abstractions.Models;

public sealed record WaitRecord(
    string WaitId,
    string EventName,
    string CorrelationId,
    string? BranchId,
    DateTimeOffset RegisteredAt,
    WaitStatus Status,
    WaitMode Mode);
