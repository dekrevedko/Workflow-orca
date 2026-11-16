using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public enum DagRunLeasePurposeV1
{
    Admission,
    ChildJoin,
    RunCancellation,
    IntegrityReconciliation
}
