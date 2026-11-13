namespace OrcaCore.Abstractions.Durable.Dag;

public enum DagNodeStateV1
{
    Pending,
    Ready,
    StartAuthorized,
    CancellationRequested,
    Succeeded,
    Failed,
    Cancelled,
    DependencyBlocked
}
