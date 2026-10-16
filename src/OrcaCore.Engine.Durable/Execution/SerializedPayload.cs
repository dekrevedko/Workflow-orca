namespace OrcaCore.Engine.Durable.Execution;

internal sealed record SerializedPayload(string ContentType, byte[] Payload);
