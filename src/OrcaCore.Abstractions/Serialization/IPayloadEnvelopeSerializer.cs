namespace OrcaCore.Abstractions.Serialization;

public interface IPayloadEnvelopeSerializer
{
    Result<SerializedPayloadEnvelope> Serialize(object? value, Type declaredType);

    Result<object?> Deserialize(DispatchPayload payload, string typeKey, Type targetType);
}
