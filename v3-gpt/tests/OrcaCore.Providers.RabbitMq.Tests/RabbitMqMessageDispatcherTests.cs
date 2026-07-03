using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.RabbitMq;
using Xunit;

namespace OrcaCore.Providers.RabbitMq.Tests;

public sealed class RabbitMqMessageDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_PublishConfirmed_ReturnsSuccess()
    {
        var publisher = new StubRabbitMqPublisher(RabbitMqPublishOutcome.Confirmed);
        IMessageDispatcher dispatcher = new RabbitMqMessageDispatcher(publisher);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.Success);
    }

    [Fact]
    public async Task DispatchAsync_TransientBrokerFailure_ReturnsRetryableFailure()
    {
        var publisher = new StubRabbitMqPublisher(RabbitMqPublishOutcome.RetryableFailure);
        IMessageDispatcher dispatcher = new RabbitMqMessageDispatcher(publisher);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.RetryableFailure);
    }

    [Fact]
    public async Task DispatchAsync_UnroutablePermanentMessage_ReturnsPermanentFailure()
    {
        var publisher = new StubRabbitMqPublisher(RabbitMqPublishOutcome.PermanentFailure);
        IMessageDispatcher dispatcher = new RabbitMqMessageDispatcher(publisher);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.PermanentFailure);
    }

    [Fact]
    public void AddOrcaCoreRabbitMq_RegistersDispatcherFromProviderPackage()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMessageDispatcher>(new StubDispatcher());

        services.AddOrcaCoreRabbitMq(new RabbitMqMessageDispatcherOptions
        {
            ConnectionString = "amqp://guest:guest@localhost:5672/",
            ExchangeName = "orca"
        });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMessageDispatcher>().Should().BeOfType<RabbitMqMessageDispatcher>();
    }

    private static OutboxWrite Record()
    {
        return new OutboxWrite(
            OutboxRecordId.New(),
            "workflow.completed",
            [1, 2, 3]);
    }

    private sealed class StubRabbitMqPublisher(RabbitMqPublishOutcome outcome) : IRabbitMqPublisher
    {
        public Task<RabbitMqPublishOutcome> PublishAsync(
            RabbitMqOutboundMessage message,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(outcome);
        }
    }

    private sealed class StubDispatcher : IMessageDispatcher
    {
        public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
