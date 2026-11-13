using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunNodeChangeV1(
    int Ordinal,
    DagNodeStateV1 State,
    InstanceId? ChildInstanceId,
    DagRunValueV1? InputWrite,
    DagRunValueV1? OutputWrite,
    string? FailureCode,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
