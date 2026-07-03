using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;

namespace OrcaCore.Providers.ZeroMq.Tests;

public sealed class ZeroMqDispatcherCertificationTests : MessageDispatcherCertificationTests
{
    protected override IReadOnlyList<DispatchResult> SupportedResults { get; } =
    [
        DispatchResult.Success,
        DispatchResult.RetryableFailure
    ];

    protected override IMessageDispatcher CreateDispatcher(DispatchResult result)
    {
        return new ZeroMqMessageDispatcher(
            new StubPublisher(result),
            new ZeroMqMessageDispatcherOptions
            {
                Endpoint = "tcp://127.0.0.1:5555",
                Topic = "orcacore.outbox"
            });
    }

    private sealed class StubPublisher(DispatchResult result) : IZeroMqPublisher
    {
        public Task<ZeroMqPublishOutcome> PublishAsync(
            ZeroMqOutboundMessage message,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(result == DispatchResult.Success
                ? ZeroMqPublishOutcome.Accepted
                : ZeroMqPublishOutcome.PeerUnavailable);
        }
    }
}
