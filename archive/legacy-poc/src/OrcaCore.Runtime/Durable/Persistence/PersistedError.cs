namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedError(
    string ExceptionType,
    string Message,
    string StepId,
    DateTimeOffset Timestamp);
