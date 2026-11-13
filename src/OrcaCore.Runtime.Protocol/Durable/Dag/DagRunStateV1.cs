namespace OrcaCore.Abstractions.Durable.Dag;

public enum DagRunStateV1
{
    Running,
    CancellationRequested,
    Succeeded,
    Failed,
    Cancelled,
    IntegrityFault
}
