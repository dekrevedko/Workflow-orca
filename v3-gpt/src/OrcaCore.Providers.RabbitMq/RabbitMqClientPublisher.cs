using System.Net.Sockets;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Publishes messages to RabbitMQ using publisher confirmations.
/// </summary>
public sealed class RabbitMqClientPublisher(RabbitMqMessageDispatcherOptions options) : IRabbitMqPublisher
{
    /// <inheritdoc />
    public async Task<RabbitMqPublishOutcome> PublishAsync(
        RabbitMqOutboundMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(options.ConnectionString)
            };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var channel = await connection
                .CreateChannelAsync(
                    new CreateChannelOptions(
                        publisherConfirmationsEnabled: true,
                        publisherConfirmationTrackingEnabled: true,
                        outstandingPublisherConfirmationsRateLimiter: null,
                        consumerDispatchConcurrency: null),
                    cancellationToken)
                .ConfigureAwait(false);
            await channel
                .BasicPublishAsync(
                    options.ExchangeName,
                    message.RoutingKey,
                    options.Mandatory,
                    new BasicProperties(),
                    message.Payload,
                    cancellationToken)
                .ConfigureAwait(false);

            return RabbitMqPublishOutcome.Confirmed;
        }
        catch (PublishException exception) when (exception.IsReturn)
        {
            return RabbitMqPublishOutcome.PermanentFailure;
        }
        catch (Exception exception) when (IsRetryable(exception))
        {
            return RabbitMqPublishOutcome.RetryableFailure;
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        return exception is BrokerUnreachableException
            or ConnectFailureException
            or AlreadyClosedException
            or IOException
            or SocketException
            or TimeoutException;
    }
}
