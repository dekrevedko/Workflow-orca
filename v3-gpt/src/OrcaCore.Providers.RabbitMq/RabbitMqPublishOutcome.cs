namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Describes the RabbitMQ publisher outcome before it is mapped to the durable dispatch contract.
/// </summary>
public enum RabbitMqPublishOutcome
{
    /// <summary>
    /// The broker confirmed the publish.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The publish failed in a way that can be retried.
    /// </summary>
    RetryableFailure,

    /// <summary>
    /// The publish failed permanently.
    /// </summary>
    PermanentFailure
}
