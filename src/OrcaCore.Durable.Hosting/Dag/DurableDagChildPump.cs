using System.Text.Json;
using System.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Dag;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Durable.Hosting.Dag;

internal interface IDurableDagChildHandler
{
    Task<bool> HandleAsync(OutboxWrite record, CancellationToken token);
    Task<bool> RecordFaultAsync(Guid runId, CancellationToken token);
}

internal sealed class DurableDagChildPump(IWorkflowOutboxStore outbox, IDurableDagChildHandler handler,
    IOutboxPumpObserver? observer = null)
{
    internal async Task<int> PumpOnceAsync(OutboxClaimRequest request, CancellationToken token)
    {
        var writes = await outbox.ClaimAsync(request with
        {
            KindSelector = OutboxKindSelector.Including(DurableDagKinds.Start, DurableDagKinds.Join, DurableDagKinds.Cancel),
            RecipientHostId = null
        }, token).ConfigureAwait(false);
        var completed = 0;
        var telemetry = new DurableDagPumpTelemetry(observer);
        foreach (var write in writes)
        {
            if (!DurableDagKinds.IsChild(write.Kind))
            {
                await outbox.ReleaseAsync(write.OutboxRecordId, token).ConfigureAwait(false);
                continue;
            }
            var watch = Stopwatch.StartNew();
            var outcome = DispatchResult.RetryableFailure;
            Exception? error = null;
            try
            {
                if (write.DagRunOwner is not { } owner || owner == Guid.Empty)
                    throw new InvalidDataException("Missing trusted DAG run owner.");
                if (await handler.HandleAsync(write, token).ConfigureAwait(false))
                {
                    await outbox.MarkAsync(write.OutboxRecordId, OutboxRecordState.Dispatched, token).ConfigureAwait(false);
                    completed++;
                    outcome = DispatchResult.Success;
                }
                else await outbox.ReleaseAsync(write.OutboxRecordId, token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException)
            {
                error = exception;
                // Never acknowledge success or release admission on an unreadable signal.
                // A trusted run is fenced into an integrity stop BEFORE its transport is poisoned.
                if (write.DagRunOwner is { } owner && owner != Guid.Empty &&
                    !await handler.RecordFaultAsync(owner, token).ConfigureAwait(false))
                {
                    await outbox.ReleaseAsync(write.OutboxRecordId, token).ConfigureAwait(false);
                    continue;
                }
                await outbox.MarkPoisonedAsync(write.OutboxRecordId, "DAG_PROTOCOL_INVALID", exception.Message, token).ConfigureAwait(false);
                outcome = DispatchResult.PermanentFailure;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await outbox.ReleaseAsync(write.OutboxRecordId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                error = exception;
                await outbox.ReleaseAsync(write.OutboxRecordId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            finally { await telemetry.RecordAsync(write, outcome, watch.Elapsed, error).ConfigureAwait(false); }
        }
        await telemetry.CompleteAsync(writes.Count).ConfigureAwait(false);
        return completed;
    }
}
