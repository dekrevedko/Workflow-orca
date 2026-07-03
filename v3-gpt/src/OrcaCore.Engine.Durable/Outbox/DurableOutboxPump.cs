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
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Claims and dispatches at most <paramref name="maxCount"/> outbox records.
    /// </summary>
    public async Task<int> PumpOnceAsync(int maxCount, CancellationToken cancellationToken)
    {
        return await PumpOnceAsync(
            new OutboxClaimRequest(maxCount, DateTimeOffset.UtcNow, DefaultLeaseDuration),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Claims and dispatches outbox records using a recoverable lease.
    /// </summary>
    public async Task<int> PumpOnceAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxCount);
        if (request.LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OutboxClaimRequest.LeaseDuration),
                request.LeaseDuration,
                "Lease duration must be positive.");
        }

        using var activity = OrcaCoreDiagnostics.ActivitySource.StartActivity("OrcaCore.Outbox.PumpOnce");
        activity?.SetTag("orcacore.outbox.max_count", request.MaxCount);

        var records = await outboxStore
            .ClaimAsync(request, cancellationToken)
            .ConfigureAwait(false);
        activity?.SetTag("orcacore.outbox.claimed_count", records.Count);

        var dispatched = 0;
        foreach (var record in records)
        {
            try
            {
                var result = await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
                await outboxStore
                    .MarkAsync(record.OutboxRecordId, ToState(result), cancellationToken)
                    .ConfigureAwait(false);
                dispatched++;
            }
            catch (OperationCanceledException)
            {
                await outboxStore
                    .ReleaseAsync(record.OutboxRecordId, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception)
            {
                await outboxStore
                    .MarkAsync(record.OutboxRecordId, OutboxRecordState.Retryable, CancellationToken.None)
                    .ConfigureAwait(false);
            }
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
