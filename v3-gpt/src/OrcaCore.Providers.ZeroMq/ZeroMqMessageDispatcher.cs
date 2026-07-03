using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Dispatches durable outbox records through a brokerless ZeroMQ publisher.
/// </summary>
public sealed class ZeroMqMessageDispatcher : IMessageDispatcher
{
    private readonly IZeroMqPublisher publisher;
    private readonly ZeroMqMessageDispatcherOptions options;

    /// <summary>
    /// Initializes a dispatcher with a provider-owned publisher adapter.
    /// </summary>
    public ZeroMqMessageDispatcher(IZeroMqPublisher publisher, ZeroMqMessageDispatcherOptions options)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.options = Validate(options);
    }

    /// <inheritdoc />
    public async Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var outcome = await publisher
            .PublishAsync(
                new ZeroMqOutboundMessage(
                    options.Endpoint,
                    options.Topic,
                    record.Kind,
                    [.. record.Payload]),
                cancellationToken)
            .ConfigureAwait(false);
        return outcome switch
        {
            ZeroMqPublishOutcome.Accepted => DispatchResult.Success,
            ZeroMqPublishOutcome.PeerUnavailable => DispatchResult.RetryableFailure,
            _ => DispatchResult.RetryableFailure
        };
    }

    private static ZeroMqMessageDispatcherOptions Validate(ZeroMqMessageDispatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Topic);
        if (options.SendTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.SendTimeout, "Send timeout must be positive.");
        }

        return options;
    }
}
