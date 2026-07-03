namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Publishes normalized RabbitMQ messages for the dispatcher.
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>
    /// Publishes one outbound message and reports the broker-level outcome.
    /// </summary>
    Task<RabbitMqPublishOutcome> PublishAsync(
        RabbitMqOutboundMessage message,
        CancellationToken cancellationToken);
}
