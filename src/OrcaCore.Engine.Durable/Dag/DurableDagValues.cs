using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Dag;

internal static class DurableDagValues
{
    internal static DagRunValueV1 Normalize(string key, object? value, Type declaredType)
    {
        if (value is null && declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) is null)
            throw new InvalidDataException("Null is not valid for this declared DAG value type.");
        var bytes = CoreWorkflowValueCodec.Serialize(value, declaredType);
        _ = CoreWorkflowValueCodec.Deserialize(bytes, declaredType);
        return new(true, key, CoreWorkflowValueCodec.Format, bytes, DurableDagWire.ValueFingerprint(bytes));
    }

    internal static byte[] VerifiedBytes(DagRunValueV1 value, string key)
    {
        var bytes = value.Bytes;
        if (!value.IsPresent || value.ContractKey != key || value.CodecFormat != CoreWorkflowValueCodec.Format ||
            value.Fingerprint != DurableDagWire.ValueFingerprint(bytes))
            throw new InvalidDataException("DAG committed value has an invalid key, format or fingerprint.");
        return bytes;
    }

    internal static object? DecodeVerified(byte[] bytes, Type declaredType) =>
        CoreWorkflowValueCodec.Deserialize(bytes, declaredType);
}
