namespace OrcaCore.Abstractions.Durable.Dag;

public enum DagRunRecordKindV1
{
    Created,
    InputAdmitted,
    MappingFailed,
    ChildJoined,
    CancellationRequested,
    Terminal,
    IntegrityFaulted,
    PoisonReconciled
}
