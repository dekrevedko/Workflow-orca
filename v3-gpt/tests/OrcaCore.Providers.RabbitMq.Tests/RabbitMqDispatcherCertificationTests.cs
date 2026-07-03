using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;

namespace OrcaCore.Providers.RabbitMq.Tests;

public sealed class RabbitMqDispatcherCertificationTests : MessageDispatcherCertificationTests
{
    protected override IMessageDispatcher CreateDispatcher(DispatchResult result)
    {
        return new RabbitMqMessageDispatcher(new StubPublisher(result));
    }

    private sealed class StubPublisher(DispatchResult result) : IRabbitMqPublisher
    {
        public Task<RabbitMqPublishOutcome> PublishAsync(
            RabbitMqOutboundMessage message,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result switch
            {
                DispatchResult.Success => RabbitMqPublishOutcome.Confirmed,
                DispatchResult.RetryableFailure => RabbitMqPublishOutcome.RetryableFailure,
                DispatchResult.PermanentFailure => RabbitMqPublishOutcome.PermanentFailure,
                _ => throw new InvalidOperationException($"Unsupported dispatch result '{result}'.")
            });
        }
    }
}
