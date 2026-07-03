using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.ZeroMq;
using Xunit;

namespace OrcaCore.Providers.ZeroMq.Tests;

public sealed class ZeroMqMessageDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_PeerAcceptsMessage_ReturnsSuccess()
    {
        var publisher = new RecordingPublisher(ZeroMqPublishOutcome.Accepted);
        var dispatcher = new ZeroMqMessageDispatcher(publisher, new ZeroMqMessageDispatcherOptions
        {
            Endpoint = "tcp://127.0.0.1:5555",
            Topic = "orcacore.outbox"
        });

        var result = await dispatcher.DispatchAsync(Outbox(), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.Success);
        publisher.Messages.Should().ContainSingle()
            .Which.Topic.Should().Be("orcacore.outbox");
    }

    [Fact]
    public async Task DispatchAsync_PeerUnavailable_ReturnsRetryableFailure()
    {
        var dispatcher = new ZeroMqMessageDispatcher(
            new RecordingPublisher(ZeroMqPublishOutcome.PeerUnavailable),
            new ZeroMqMessageDispatcherOptions
            {
                Endpoint = "tcp://127.0.0.1:5555",
                Topic = "orcacore.outbox"
            });

        var result = await dispatcher.DispatchAsync(Outbox(), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.RetryableFailure);
    }

    [Fact]
    public void Readme_DocumentsBrokerlessDeliveryEnvelope()
    {
        var readme = File.ReadAllText(ProviderReadmePath());

        readme.Should().Contain("at-least-once outbox intent");
        readme.Should().Contain("brokerless");
        readme.Should().Contain("weaker");
        readme.Should().NotContain("publisher confirms");
        readme.Should().NotContain("dead-letter");
    }

    private static OutboxWrite Outbox()
    {
        return new OutboxWrite(
            OutboxRecordId.New(),
            "external-message",
            [1, 2, 3]);
    }

    private static string ProviderReadmePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OrcaCore.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return Path.Combine(directory!.FullName, "src", "OrcaCore.Providers.ZeroMq", "README.md");
    }

    private sealed class RecordingPublisher(ZeroMqPublishOutcome outcome) : IZeroMqPublisher
    {
        public List<ZeroMqOutboundMessage> Messages { get; } = [];

        public Task<ZeroMqPublishOutcome> PublishAsync(
            ZeroMqOutboundMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(outcome);
        }
    }
}
