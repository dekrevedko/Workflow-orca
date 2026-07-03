using NetMQ;
using NetMQ.Sockets;

namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Publishes brokerless messages with a NetMQ push socket.
/// </summary>
public sealed class NetMqPublisher : IZeroMqPublisher, IDisposable
{
    private readonly PushSocket socket;
    private readonly TimeSpan sendTimeout;

    /// <summary>
    /// Initializes a NetMQ publisher connected to the configured endpoint.
    /// </summary>
    public NetMqPublisher(ZeroMqMessageDispatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        socket = new PushSocket();
        socket.Connect(options.Endpoint);
        sendTimeout = options.SendTimeout;
    }

    /// <inheritdoc />
    public Task<ZeroMqPublishOutcome> PublishAsync(
        ZeroMqOutboundMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var netMqMessage = new NetMQMessage();
        netMqMessage.Append(message.Topic);
        netMqMessage.Append(message.Kind);
        netMqMessage.Append(message.Payload);
        var accepted = socket.TrySendMultipartMessage(sendTimeout, netMqMessage);
        return Task.FromResult(accepted ? ZeroMqPublishOutcome.Accepted : ZeroMqPublishOutcome.PeerUnavailable);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        socket.Dispose();
    }
}
