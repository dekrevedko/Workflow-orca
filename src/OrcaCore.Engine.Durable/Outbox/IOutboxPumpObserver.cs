using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Outbox;

/// <summary>
/// Observes durable outbox pump dispatch and cycle results for metrics and diagnostics.
/// </summary>
internal interface IOutboxPumpObserver
{
    /// <summary>
    /// Called after one outbox record dispatch attempt completes.
    /// </summary>
    ValueTask OnDispatchCompletedAsync(
        OutboxDispatchObservation observation,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Called after a pump cycle has claimed and processed all available records.
    /// </summary>
    ValueTask OnPumpCompletedAsync(OutboxPumpObservation observation, CancellationToken cancellationToken);
}

/// <summary>
/// Describes one completed durable outbox pump cycle.
/// </summary>
internal sealed record OutboxPumpObservation(
    int ClaimedCount,
    int DispatchAttemptCount,
    int SuccessCount,
    int RetryableFailureCount,
    int PermanentFailureCount);

/// <summary>
/// Describes one durable outbox dispatch attempt.
/// </summary>
internal sealed record OutboxDispatchObservation(
    string Kind,
    OutboxRecordId OutboxRecordId,
    int Attempt,
    DispatchResult Result,
    TimeSpan Duration,
    Exception? Exception = null);
