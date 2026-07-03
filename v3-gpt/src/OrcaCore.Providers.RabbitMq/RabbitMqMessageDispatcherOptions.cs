namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Configures RabbitMQ dispatch target metadata.
/// </summary>
public sealed record RabbitMqMessageDispatcherOptions
{
    /// <summary>
    /// Gets the AMQP connection string for the RabbitMQ broker.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Gets the exchange name used for dispatched outbox records.
    /// </summary>
    public string ExchangeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether publishes require routing to at least one queue.
    /// </summary>
    public bool Mandatory { get; init; } = true;
}
