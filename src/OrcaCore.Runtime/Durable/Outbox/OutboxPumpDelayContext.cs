namespace OrcaCore.Runtime.Durable.Outbox;

public sealed record OutboxPumpDelayContext(
    int ConsecutiveFailures,
    int DispatchedCount,
    TimeSpan PollingInterval,
    Exception? LastError);
