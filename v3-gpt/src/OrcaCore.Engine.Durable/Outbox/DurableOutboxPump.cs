using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Outbox;

/// <summary>
/// Claims committed durable outbox records and dispatches them through the configured dispatcher.
/// </summary>
public sealed class DurableOutboxPump(
    IWorkflowOutboxStore outboxStore,
    IMessageDispatcher dispatcher)
{
    /// <summary>
    /// Claims and dispatches at most <paramref name="maxCount"/> outbox records.
    /// </summary>
    public async Task<int> PumpOnceAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        using var activity = OrcaCoreDiagnostics.ActivitySource.StartActivity("OrcaCore.Outbox.PumpOnce");
        activity?.SetTag("orcacore.outbox.max_count", maxCount);

        var records = await outboxStore
            .ClaimAsync(maxCount, cancellationToken)
            .ConfigureAwait(false);
        activity?.SetTag("orcacore.outbox.claimed_count", records.Count);

        var dispatched = 0;
        foreach (var record in records)
        {
            var result = await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
            await outboxStore
                .MarkAsync(record.OutboxRecordId, ToState(result), cancellationToken)
                .ConfigureAwait(false);
            dispatched++;
        }

        activity?.SetTag("orcacore.outbox.dispatched_count", dispatched);
        return dispatched;
    }

    private static OutboxRecordState ToState(DispatchResult result)
    {
        return result switch
        {
            DispatchResult.Success => OutboxRecordState.Dispatched,
            DispatchResult.RetryableFailure => OutboxRecordState.Retryable,
            DispatchResult.PermanentFailure => OutboxRecordState.Poisoned,
            _ => throw new UnreachableException()
        };
    }
}
