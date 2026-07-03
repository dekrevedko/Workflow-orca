using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Dispatches durable outbox records to RabbitMQ through a publisher seam.
/// </summary>
public sealed class RabbitMqMessageDispatcher(IRabbitMqPublisher publisher) : IMessageDispatcher
{
    /// <inheritdoc />
    public async Task<DispatchResult> DispatchAsync(
        OutboxWrite record,
        CancellationToken cancellationToken)
    {
        var outcome = await publisher
            .PublishAsync(ToMessage(record), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            RabbitMqPublishOutcome.Confirmed => DispatchResult.Success,
            RabbitMqPublishOutcome.RetryableFailure => DispatchResult.RetryableFailure,
            RabbitMqPublishOutcome.PermanentFailure => DispatchResult.PermanentFailure,
            _ => throw new InvalidOperationException($"Unknown RabbitMQ publish outcome '{outcome}'.")
        };
    }

    private static RabbitMqOutboundMessage ToMessage(OutboxWrite record)
    {
        return new RabbitMqOutboundMessage(
            record.Kind,
            record.Payload);
    }
}
