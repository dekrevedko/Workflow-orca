using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace OrcaCore.Providers.RabbitMq.Tests;

public sealed class RabbitMqDispatcherIntegrationTests : IAsyncLifetime
{
    private const string ExchangeName = "orca.dispatch";
    private const string QueueName = "orca.dispatch.queue";
    private const string RoutingKey = "workflow.completed";

    private readonly RabbitMqContainer container = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await DeclareTopologyAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    [Fact]
    public async Task DispatchAsync_RealBrokerConfirmedPublish_DeliversMessage()
    {
        var dispatcher = CreateDispatcher(container.GetConnectionString());

        var result = await dispatcher.DispatchAsync(Record(RoutingKey, [4, 5, 6]), TestContext.Current.CancellationToken);
        var delivered = await BasicGetAsync(TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.Success);
        delivered.Should().NotBeNull();
        delivered!.Body.ToArray().Should().Equal([4, 5, 6]);
        delivered.RoutingKey.Should().Be(RoutingKey);
    }

    [Fact]
    public async Task DispatchAsync_BrokerUnavailable_ReturnsRetryableFailure()
    {
        var dispatcher = CreateDispatcher("amqp://guest:guest@127.0.0.1:1/");

        var result = await dispatcher.DispatchAsync(Record(RoutingKey, [1]), TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.RetryableFailure);
    }

    [Fact]
    public async Task DispatchAsync_PermanentRoutingFailure_IsDocumentedAndReturned()
    {
        var dispatcher = CreateDispatcher(container.GetConnectionString());

        var result = await dispatcher.DispatchAsync(Record("missing.route", [9]), TestContext.Current.CancellationToken);
        var readme = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/OrcaCore.Providers.RabbitMq/README.md"),
            TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.PermanentFailure);
        readme.Should().Contain("publisher confirms");
        readme.Should().Contain("at-least-once");
        readme.Should().Contain("no exactly-once delivery");
    }

    private static RabbitMqMessageDispatcher CreateDispatcher(string connectionString)
    {
        return new RabbitMqMessageDispatcher(
            new RabbitMqClientPublisher(
                new RabbitMqMessageDispatcherOptions
                {
                    ConnectionString = connectionString,
                    ExchangeName = ExchangeName,
                    Mandatory = true
                }));
    }

    private async Task DeclareTopologyAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(container.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            QueueName,
            ExchangeName,
            RoutingKey,
            cancellationToken: cancellationToken);
    }

    private async Task<BasicGetResult?> BasicGetAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(container.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        return await channel.BasicGetAsync(QueueName, autoAck: true, cancellationToken);
    }

    private static OutboxWrite Record(string kind, byte[] payload)
    {
        return new OutboxWrite(OutboxRecordId.New(), kind, payload);
    }
}
