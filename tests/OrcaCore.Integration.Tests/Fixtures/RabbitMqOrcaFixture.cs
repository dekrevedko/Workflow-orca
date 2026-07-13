using OrcaCore.Providers.RabbitMq;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace OrcaCore.Integration.Tests.Fixtures;

public sealed class RabbitMqOrcaFixture : IAsyncLifetime
{
    public const string ExchangeName = "orca.integration";
    public const string QueueName = "orca.integration.queue";
    public const string RoutingKey = "workflow.event";

    private readonly RabbitMqContainer container = new RabbitMqBuilder("rabbitmq:4-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        await DeclareTopologyAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public RabbitMqMessageDispatcherOptions CreateDispatcherOptions()
    {
        return new RabbitMqMessageDispatcherOptions
        {
            ConnectionString = ConnectionString,
            ExchangeName = ExchangeName,
            Mandatory = true
        };
    }

    public RabbitMqMessageDispatcher CreateDispatcher()
    {
        return new RabbitMqMessageDispatcher(
            new RabbitMqClientPublisher(CreateDispatcherOptions()));
    }

    public async Task<BasicGetResult?> BasicGetAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        return await channel.BasicGetAsync(QueueName, autoAck: true, cancellationToken);
    }

    public async Task PurgeQueueAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueuePurgeAsync(QueueName, cancellationToken);
    }

    private async Task DeclareTopologyAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory { Uri = new Uri(ConnectionString) };
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
        foreach (var routingKey in new[]
                 {
                     "external-job-start",
                     "external-job-stop",
                     "lifecycle-event",
                     "child-start",
                     "workflow.completed"
                 })
        {
            await channel.QueueBindAsync(
                QueueName,
                ExchangeName,
                routingKey,
                cancellationToken: cancellationToken);
        }
    }
}

[CollectionDefinition(nameof(RabbitMqCollection), DisableParallelization = true)]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqOrcaFixture>;
