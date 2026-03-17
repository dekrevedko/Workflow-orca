using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Outbox;

internal sealed class DurableOutboxPump(
    IWorkflowStore store,
    DurableWorkflowEngineOptions options)
{
    public Task<OutboxDispatchResult> DispatchPendingAsync(
        IOutboxDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return DispatchCoreAsync(dispatcher.DispatchAsync, observer: null, cancellationToken);
    }

    public Task? StartIfConfigured(CancellationToken cancellationToken)
    {
        if (!options.AutoDispatchOutbox || options.OutboxDispatcher is null)
            return null;

        return Task.Run(() => RunAsync(cancellationToken), cancellationToken);
    }

    private async Task<OutboxDispatchResult> DispatchCoreAsync(
        Func<OutboxRecord, CancellationToken, Task> dispatch,
        IOutboxPumpObserver? observer,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pending = await store.GetPendingOutboxAsync(cancellationToken);
        var results = new List<OutboxDispatchItemResult>(pending.Count);

        foreach (var record in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await dispatch(record, cancellationToken);
                await store.MarkOutboxDispatchedAsync(record.OutboxId, cancellationToken);
                results.Add(new OutboxDispatchItemResult(record, Succeeded: true, Error: null));
                InvokeObserver(() => observer?.OnDispatchSucceeded(record));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var attempts = record.FailureCount + 1;
                var poisoned = attempts >= options.MaxOutboxDispatchAttempts;
                var updated = await store.RecordOutboxDispatchFailureAsync(
                    record.OutboxId,
                    ex.Message,
                    DateTimeOffset.UtcNow,
                    poisoned,
                    cancellationToken);
                results.Add(new OutboxDispatchItemResult(updated, Succeeded: false, Error: ex));
                InvokeObserver(() => observer?.OnDispatchFailed(updated, ex));
                if (poisoned && options.OutboxPoisonHandler is not null)
                    await InvokePoisonHandlerAsync(updated, ex, cancellationToken);
            }
        }

        return new OutboxDispatchResult(
            pending.Count,
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
                    options.OutboxDispatcher!.DispatchAsync,
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
}
