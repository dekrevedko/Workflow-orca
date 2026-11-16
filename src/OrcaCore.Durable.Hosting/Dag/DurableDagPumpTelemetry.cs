using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagPumpTelemetry(IOutboxPumpObserver? observer)
{
    private int attempts;
    private int successes;
    private int retryable;
    private int permanent;

    internal async ValueTask RecordAsync(OutboxWrite write, DispatchResult result, TimeSpan duration, Exception? error)
    {
        attempts++;
        if (result == DispatchResult.Success) successes++;
        else if (result == DispatchResult.PermanentFailure) permanent++;
        else retryable++;
        if (observer is null) return;
        try
        {
            await observer.OnDispatchCompletedAsync(new(write.Kind, write.OutboxRecordId, write.DispatchAttempt,
                result, duration, error), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) { /* Instrumentation cannot turn an acknowledged durable transition into a retry. */ }
    }

    internal async ValueTask CompleteAsync(int claimed)
    {
        if (observer is null) return;
        try
        {
            await observer.OnPumpCompletedAsync(new(claimed, attempts, successes, retryable, permanent),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception) { /* Best-effort telemetry, never persistence authority. */ }
    }
}
