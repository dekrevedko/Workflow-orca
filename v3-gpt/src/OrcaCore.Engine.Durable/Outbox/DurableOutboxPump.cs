using System.Threading.Channels;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Outbox;

internal sealed class DurableOutboxPump(
    IWorkflowOutboxStore outboxStore,
    IMessageDispatcher dispatcher)
{
    internal async Task<int> PumpOnceAsync(int maxCount, CancellationToken cancellationToken)
    {
        var records = await outboxStore
            .ClaimAsync(maxCount, cancellationToken)
            .ConfigureAwait(false);
        var channel = Channel.CreateUnbounded<OutboxWrite>();
        foreach (var record in records)
        {
            await channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
        }

        channel.Writer.Complete();

        var dispatched = 0;
        await foreach (var record in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var result = await dispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
            await outboxStore
                .MarkAsync(record.OutboxRecordId, ToState(result), cancellationToken)
                .ConfigureAwait(false);
            dispatched++;
        }

        return dispatched;
    }

    private static OutboxRecordState ToState(DispatchResult result)
    {
        return result switch
        {
            DispatchResult.Success => OutboxRecordState.Dispatched,
            DispatchResult.RetryableFailure => OutboxRecordState.Retryable,
            DispatchResult.PermanentFailure => OutboxRecordState.Poisoned,
            _ => throw new InvalidOperationException($"Unknown dispatch result '{result}'.")
        };
    }
}
