using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunRecordV1(
    int Version,
    DagRunRecordKindV1 Kind,
    Guid RunId,
    Guid OperationId,
    StreamVersion StreamVersion,
    DagRunStateV1 State,
    DagRunSnapshotV1? CreationSnapshot,
    IReadOnlyList<DagRunNodeChangeV1> ChangedNodes,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt)
{
    private IReadOnlyList<DagRunNodeChangeV1> changes = Array.AsReadOnly(ChangedNodes.ToArray());

    public IReadOnlyList<DagRunNodeChangeV1> ChangedNodes
    {
        get => changes;
        init => changes = Array.AsReadOnly(value.ToArray());
    }
}
