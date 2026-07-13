namespace OrcaCore.Runtime.Durable.Outbox;

public sealed record OutboxPumpCycleResult(
    int DispatchedCount,
    int ConsecutiveFailures,
    TimeSpan NextDelay,
    Exception? Failure);
