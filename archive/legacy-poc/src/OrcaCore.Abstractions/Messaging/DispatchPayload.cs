namespace OrcaCore.Abstractions.Messaging;

public sealed record DispatchPayload(
    ReadOnlyMemory<byte> Body,
    string ContentType,
    string SchemaId);
