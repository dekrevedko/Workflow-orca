using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunCheckpointV1(
    int Version,
    Guid RunId,
    StreamVersion StreamVersion,
    string ContentType,
    byte[] Payload,
    string Sha256)
{
    private byte[] payload = Payload.ToArray();

    public byte[] Payload
    {
        get => payload.ToArray();
        init => payload = value.ToArray();
    }
}
