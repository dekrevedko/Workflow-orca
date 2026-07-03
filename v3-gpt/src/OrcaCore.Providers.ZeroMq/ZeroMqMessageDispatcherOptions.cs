namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Configures brokerless ZeroMQ outbox dispatch.
/// </summary>
public sealed record ZeroMqMessageDispatcherOptions
{
    /// <summary>
    /// Gets the ZeroMQ endpoint used by the publisher socket.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Gets the logical topic frame sent before the outbox payload.
    /// </summary>
    public required string Topic { get; init; }

    /// <summary>
    /// Gets how long the publisher waits for socket acceptance before returning retryable failure.
    /// </summary>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(1);
}
