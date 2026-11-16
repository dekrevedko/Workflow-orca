namespace OrcaCore.Abstractions.Durable.Dag;

/// <summary>Detached fixed-codec bytes and their verified canonical storage fingerprint.</summary>
public sealed record DagRunValueV1(
    bool IsPresent,
    string ContractKey,
    string CodecFormat,
    byte[] Bytes,
    string Fingerprint)
{
    private byte[] bytes = DagRunValidation.ValueBytes(IsPresent, ContractKey, CodecFormat, Bytes, Fingerprint);

    public byte[] Bytes
    {
        get => bytes.ToArray();
        init => bytes = DagRunValidation.ValueBytes(IsPresent, ContractKey, CodecFormat, value, Fingerprint);
    }
}
