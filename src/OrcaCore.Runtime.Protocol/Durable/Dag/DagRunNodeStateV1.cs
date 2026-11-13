using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable.Dag;

public sealed record DagRunNodeStateV1(
    int Ordinal,
    DagNodeStateV1 State,
    InstanceId? ChildInstanceId,
    DagRunValueV1? Input,
    DagRunValueV1? Output,
    string? FailureCode,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
