namespace OrcaCore.Abstractions.Messaging;

public sealed record DispatchOutcome(
    bool Succeeded,
    bool Retryable,
    string? Error);
