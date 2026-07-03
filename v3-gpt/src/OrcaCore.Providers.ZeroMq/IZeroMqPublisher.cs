namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Publishes a brokerless ZeroMQ message through a provider-owned socket adapter.
/// </summary>
public interface IZeroMqPublisher
{
    /// <summary>
    /// Sends one outbound message and reports whether a peer accepted it.
    /// </summary>
    Task<ZeroMqPublishOutcome> PublishAsync(
        ZeroMqOutboundMessage message,
        CancellationToken cancellationToken);
}
