using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunSnapshotV1(
    Guid RunId,
    StreamVersion StreamVersion,
    DagRunStartKeyV1 StartKey,
    DagRunGraphV1 Graph,
    DagRunValueV1 RunInput,
    DagRunStateV1 State,
    IReadOnlyList<DagRunNodeStateV1> Nodes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt)
{
    private IReadOnlyList<DagRunNodeStateV1> nodes = Array.AsReadOnly(Nodes.ToArray());

    public IReadOnlyList<DagRunNodeStateV1> Nodes
    {
        get => nodes;
        init => nodes = Array.AsReadOnly(value.ToArray());
    }
}
