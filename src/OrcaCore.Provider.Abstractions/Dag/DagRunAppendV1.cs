using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunAppendV1(
    Guid OperationId,
    string OperationFingerprint,
    DagRunFenceV1? Fence,
    IReadOnlyList<DagRunRecordV1> Records,
    DagRunCheckpointV1 Checkpoint)
{
    private IReadOnlyList<DagRunRecordV1> records = Array.AsReadOnly(Records.ToArray());

    public IReadOnlyList<DagRunRecordV1> Records
    {
        get => records;
        init => records = Array.AsReadOnly(value.ToArray());
    }
}
