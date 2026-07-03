namespace OrcaCore.Providers.ZeroMq;

/// <summary>
/// Describes one brokerless ZeroMQ outbound message.
/// </summary>
public sealed record ZeroMqOutboundMessage(string Endpoint, string Topic, string Kind, byte[] Payload);
