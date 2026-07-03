namespace OrcaCore.Engine.Durable.Outbox;

/// <summary>
/// Observes completed durable outbox pump cycles for metrics and diagnostics.
/// </summary>
public interface IOutboxPumpObserver
{
    /// <summary>
    /// Called after a pump cycle has claimed and processed all available records.
    /// </summary>
    ValueTask OnPumpCompletedAsync(OutboxPumpObservation observation, CancellationToken cancellationToken);
}

/// <summary>
/// Describes one completed durable outbox pump cycle.
/// </summary>
public sealed record OutboxPumpObservation(
    int ClaimedCount,
    int DispatchAttemptCount,
    int SuccessCount,
    int RetryableFailureCount,
    int PermanentFailureCount);
