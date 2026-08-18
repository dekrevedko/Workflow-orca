using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.Engine.Durable.Outbox;

/// <summary>
/// Claims committed durable outbox records and dispatches them through the configured dispatcher.
/// </summary>
internal sealed class DurableOutboxPump(
    IWorkflowOutboxStore outboxStore,
    IMessageDispatcher dispatcher,
    IOutboxPumpObserver? observer = null,
    TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Claims and dispatches at most <paramref name="maxCount"/> outbox records.
    /// </summary>
    public async Task<int> PumpOnceAsync(int maxCount, CancellationToken cancellationToken)
    {
        return await PumpOnceAsync(
            new OutboxClaimRequest(maxCount, timeProvider.GetUtcNow(), DefaultLeaseDuration),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// DR-037: the legacy dispatcher pump excludes every runtime-owned or application-event lane.
    /// A caller-supplied selector is honored, but continuation records remain unconditionally
    /// internal and any provider record outside the selected lane is released rather than dispatched.
    /// </summary>
    private static OutboxClaimRequest SelectDispatchLane(OutboxClaimRequest request)
    {
        return request.KindSelector is null
            ? request with
            {
                KindSelector = OutboxKindSelector.Excluding(
                    OutboxKinds.Continue,
                    OutboxKinds.WorkflowEvent)
            }
            : request;
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

        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity(
            OrcaCoreDiagnostics.OutboxPumpCycleActivity);
        activity?.SetTag(OrcaCoreDiagnostics.OutboxMaxCountKey, request.MaxCount);

        var dispatchRequest = SelectDispatchLane(request);
        var records = await outboxStore
            .ClaimAsync(dispatchRequest, cancellationToken)
            .ConfigureAwait(false);
        activity?.SetTag(OrcaCoreDiagnostics.OutboxClaimedCountKey, records.Count);

        var dispatched = 0;
        var dispatchAttempts = 0;
        var successes = 0;
        var retryableFailures = 0;
        var permanentFailures = 0;
        foreach (var record in records)
        {
            if (string.Equals(record.Kind, OutboxKinds.Continue, StringComparison.Ordinal) ||
                dispatchRequest.KindSelector is { } selector && !selector.Matches(record.Kind))
            {
                await outboxStore
                    .ReleaseAsync(record.OutboxRecordId, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            var dispatchStopwatch = Stopwatch.StartNew();
            using var dispatchActivity =
                OrcaCoreDurableDiagnostics.ActivitySource.StartActivity(
                    OrcaCoreDiagnostics.OutboxDispatchActivity);
            dispatchActivity?.SetTag(OrcaCoreDiagnostics.OutboxKindKey, record.Kind);
            dispatchActivity?.SetTag(OrcaCoreDiagnostics.OutboxRecordIdKey, record.OutboxRecordId.ToString());
            try
            {
                dispatchAttempts++;
                var outcome = dispatcher is IDetailedOutboxMessageDispatcher detailedDispatcher
                    ? await detailedDispatcher
                        .DispatchDetailedAsync(record, cancellationToken)
                        .ConfigureAwait(false)
                    : new OutboxDispatchOutcome(
                        await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false));
                var result = outcome.Result;
                dispatchStopwatch.Stop();
                dispatchActivity?.SetTag(OrcaCoreDiagnostics.OutboxResultKey, ToResultTag(result));
                if (result is not DispatchResult.Success)
                {
                    dispatchActivity?.SetStatus(ActivityStatusCode.Error, result.ToString());
                }

                switch (result)
                {
                    case DispatchResult.Success:
                        successes++;
                        break;
                    case DispatchResult.RetryableFailure:
                        retryableFailures++;
                        break;
                    case DispatchResult.PermanentFailure:
                        permanentFailures++;
                        break;
                }

                await ObserveDispatchCompletedAsync(
                    record,
                    result,
                    dispatchStopwatch.Elapsed,
                    null,
                    cancellationToken)
                    .ConfigureAwait(false);
                if (result == DispatchResult.PermanentFailure && outcome.FailureCode is { } failureCode)
                {
                    await outboxStore
                        .MarkPoisonedAsync(
                            record.OutboxRecordId,
                            failureCode,
                            outcome.FailureDetail,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await outboxStore
                        .MarkAsync(record.OutboxRecordId, ToState(result), cancellationToken)
                        .ConfigureAwait(false);
                }
                dispatched++;
            }
            catch (OperationCanceledException)
            {
                await outboxStore
                    .ReleaseAsync(record.OutboxRecordId, CancellationToken.None)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                dispatchStopwatch.Stop();
                retryableFailures++;
                dispatchActivity?.SetTag(
                    OrcaCoreDiagnostics.OutboxResultKey,
                    OrcaCoreDiagnostics.RetryableDispatchResult);
                dispatchActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                dispatchActivity?.AddException(ex);
                await ObserveDispatchCompletedAsync(
                    record,
                    DispatchResult.RetryableFailure,
                    dispatchStopwatch.Elapsed,
                    ex,
                    CancellationToken.None)
                    .ConfigureAwait(false);
                await outboxStore
                    .MarkAsync(record.OutboxRecordId, OutboxRecordState.Retryable, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        activity?.SetTag(OrcaCoreDiagnostics.OutboxDispatchedCountKey, dispatched);
        if (observer is not null)
        {
            try
            {
                await observer
                    .OnPumpCompletedAsync(
                        new OutboxPumpObservation(
                            records.Count,
                            dispatchAttempts,
                            successes,
                            retryableFailures,
                            permanentFailures),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
            }
        }

        return dispatched;
    }

    private async ValueTask ObserveDispatchCompletedAsync(
        OutboxWrite record,
        DispatchResult result,
        TimeSpan duration,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        if (observer is null)
        {
            return;
        }

        try
        {
            await observer
                .OnDispatchCompletedAsync(
                    new OutboxDispatchObservation(
                        record.Kind,
                        record.OutboxRecordId,
                        record.DispatchAttempt,
                        result,
                        duration,
                        exception),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
        }
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

    private static string ToResultTag(DispatchResult result)
    {
        return result switch
        {
            DispatchResult.Success => OrcaCoreDiagnostics.SuccessDispatchResult,
            DispatchResult.RetryableFailure => OrcaCoreDiagnostics.RetryableDispatchResult,
            DispatchResult.PermanentFailure => OrcaCoreDiagnostics.PermanentDispatchResult,
            _ => throw new UnreachableException()
        };
    }
}
