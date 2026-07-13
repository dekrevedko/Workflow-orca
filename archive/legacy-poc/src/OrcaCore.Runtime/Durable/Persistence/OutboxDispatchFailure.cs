namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record OutboxDispatchFailure(
    string? Error,
    bool Poison,
    DateTimeOffset FailedAt,
    DateTimeOffset? NextAttemptAt);
