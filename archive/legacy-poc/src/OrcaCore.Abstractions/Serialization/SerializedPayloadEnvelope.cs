namespace OrcaCore.Abstractions.Serialization;

public sealed record SerializedPayloadEnvelope(
    DispatchPayload Payload,
    string TypeKey);
