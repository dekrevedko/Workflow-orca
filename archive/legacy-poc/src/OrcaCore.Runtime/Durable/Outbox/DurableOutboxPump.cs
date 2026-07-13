using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

internal sealed class DurableOutboxPump(
    IWorkflowStore store,
    DurableWorkflowEngineOptions options)
{
    private readonly string _leaseOwner = $"outbox-pump:{Guid.NewGuid():N}";

    public Task<OutboxDispatchResult> DispatchPendingAsync(
        IMessageDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return DispatchCoreAsync(dispatcher, observer: null, cancellationToken);
    }

    public Task? StartIfConfigured(CancellationToken cancellationToken)
    {
        if (!options.AutoDispatchOutbox || options.MessageDispatcher is null)
            return null;

        return Task.Run(() => RunAsync(cancellationToken), cancellationToken);
    }

    private async Task<OutboxDispatchResult> DispatchCoreAsync(
        IMessageDispatcher dispatcher,
        IOutboxPumpObserver? observer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest(_leaseOwner, int.MaxValue, options.OutboxLeaseDuration),
            cancellationToken);
        var results = new List<OutboxDispatchItemResult>(leased.Count);

        foreach (var record in leased)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var dispatchMessage = Map(record);
                var dispatchResult = await dispatcher.DispatchAsync(dispatchMessage, cancellationToken);
                var failed = TryGetFailure(record, dispatchResult);
                if (failed is null)
                {
                    var completed = await store.CompleteLeasedOutboxAsync(
                        record.OutboxId,
                        _leaseOwner,
                        DateTimeOffset.UtcNow,
                        cancellationToken);
                    results.Add(new OutboxDispatchItemResult(completed, Succeeded: true, Error: null));
                    InvokeObserver(() => observer?.OnDispatchSucceeded(completed));
                    continue;
                }

                var failedRecord = await store.FailLeasedOutboxAsync(
                    record.OutboxId,
                    _leaseOwner,
                    CreateFailure(record, failed, DateTimeOffset.UtcNow),
                    cancellationToken);
                results.Add(new OutboxDispatchItemResult(failedRecord, Succeeded: false, Error: failed.Exception));
                InvokeObserver(() => observer?.OnDispatchFailed(failedRecord, failed.Exception));
                if (failed.Poison && options.OutboxPoisonHandler is not null)
                    await InvokePoisonHandlerAsync(failedRecord, failed.Exception, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failedRecord = await store.FailLeasedOutboxAsync(
                    record.OutboxId,
                    _leaseOwner,
                    CreateFailure(record, new DispatchFailure(false, ex.Message, ex), DateTimeOffset.UtcNow),
                    cancellationToken);
                results.Add(new OutboxDispatchItemResult(failedRecord, Succeeded: false, Error: ex));
                InvokeObserver(() => observer?.OnDispatchFailed(failedRecord, ex));
                if (failedRecord.Poisoned && options.OutboxPoisonHandler is not null)
                    await InvokePoisonHandlerAsync(failedRecord, ex, cancellationToken);
            }
        }

        return new OutboxDispatchResult(
            leased.Count,
            results.Count(x => x.Succeeded),
            results.Count(x => !x.Succeeded),
            results);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var dispatchResult = await DispatchCoreAsync(
                    options.MessageDispatcher!,
                    options.OutboxPumpObserver,
                    cancellationToken);
                var failure = dispatchResult.Results.FirstOrDefault(x => !x.Succeeded)?.Error;
                consecutiveFailures = failure is null ? 0 : consecutiveFailures + 1;
                var nextDelay = ResolveNextDelay(consecutiveFailures, dispatchResult.SucceededCount, failure);
                InvokeObserver(() => options.OutboxPumpObserver?.OnCycleCompleted(
                    new OutboxPumpCycleResult(dispatchResult.SucceededCount, consecutiveFailures, nextDelay, failure)));

                await Task.Delay(nextDelay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var nextDelay = ResolveNextDelay(consecutiveFailures, dispatchedCount: 0, failure: ex);
                InvokeObserver(() => options.OutboxPumpObserver?.OnCycleCompleted(
                    new OutboxPumpCycleResult(0, consecutiveFailures, nextDelay, ex)));

                try
                {
                    await Task.Delay(nextDelay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private TimeSpan ResolveNextDelay(int consecutiveFailures, int dispatchedCount, Exception? failure)
    {
        if (options.OutboxPumpDelayStrategy is null)
            return options.OutboxDispatchPollingInterval;

        return options.OutboxPumpDelayStrategy.GetDelay(
            new OutboxPumpDelayContext(
                consecutiveFailures,
                dispatchedCount,
                options.OutboxDispatchPollingInterval,
                failure));
    }

    private static DispatchMessage Map(OutboxRecord record) =>
        new(
            record.OutboxId,
            record.IdempotencyKey,
            record.MessageType,
            record.Channel,
            record.Destination,
            record.PayloadEnvelope.Payload,
            record.CorrelationId,
            record.CausationEventId,
            record.InstanceId,
            record.ParentInstanceId,
            record.RootInstanceId,
            record.ResumeTokenId,
            headers: new Dictionary<string, string>(StringComparer.Ordinal));

    private OutboxDispatchFailure CreateFailure(
        OutboxRecord record,
        DispatchFailure failure,
        DateTimeOffset failedAt)
    {
        var attempt = record.AttemptCount + 1;
        var poison = failure.Poison || attempt >= options.MaxOutboxDispatchAttempts;
        return new OutboxDispatchFailure(
            failure.Error,
            poison,
            failedAt,
            poison ? null : failedAt.Add(ResolveRetryDelay(record.AttemptCount)));
    }

    private TimeSpan ResolveRetryDelay(int completedFailures)
    {
        var baseDelay = options.OutboxDispatchPollingInterval;
        if (baseDelay <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var exponent = Math.Min(Math.Max(completedFailures, 0), 30);
        var multiplier = 1L << exponent;
        var maxTicks = TimeSpan.MaxValue.Ticks;
        var delayTicks = baseDelay.Ticks > maxTicks / multiplier
            ? maxTicks
            : baseDelay.Ticks * multiplier;
        return TimeSpan.FromTicks(delayTicks);
    }

    private static DispatchFailure? TryGetFailure(
        OutboxRecord record,
        Result<DispatchOutcome> dispatchResult)
    {
        if (dispatchResult.IsFailure)
        {
            var ex = dispatchResult.Error!;
            return new DispatchFailure(Poison: false, ex.Message, ex);
        }

        var outcome = dispatchResult.Value!;
        if (outcome.Succeeded)
            return null;

        var message = outcome.Error ?? $"Dispatch of outbox record '{record.OutboxId}' failed.";
        return new DispatchFailure(
            Poison: !outcome.Retryable,
            Error: message,
            Exception: new InvalidOperationException(message));
    }

    private static void InvokeObserver(Action callback)
    {
        try
        {
            callback();
        }
        catch
        {
            // Observability hooks must not affect durable correctness.
        }
    }

    private async Task InvokePoisonHandlerAsync(OutboxRecord record, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            await options.OutboxPoisonHandler!.HandleAsync(record, exception, cancellationToken);
        }
        catch
        {
            // Poison handling hooks must not affect durable correctness.
        }
    }

    private sealed record DispatchFailure(
        bool Poison,
        string? Error,
        Exception Exception);
}
