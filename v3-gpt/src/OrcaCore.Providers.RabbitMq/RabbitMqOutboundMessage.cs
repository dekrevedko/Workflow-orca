namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Represents a normalized RabbitMQ publish request derived from an outbox record.
/// </summary>
public sealed record RabbitMqOutboundMessage(string RoutingKey, ReadOnlyMemory<byte> Payload);
