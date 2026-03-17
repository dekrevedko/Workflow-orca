
namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedWaitRecord(
    string WaitId,
    string EventName,
    string CorrelationId,
    string? BranchId,
    DateTimeOffset RegisteredAt,
    WaitStatus Status,
    WaitMode Mode);
