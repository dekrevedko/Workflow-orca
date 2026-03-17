namespace OrcaCore.Runtime.Durable.Outbox;

public sealed record OutboxDispatchResult(
    int AttemptedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<OutboxDispatchItemResult> Results);
