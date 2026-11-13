using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public enum DagRunLeaseOutcomeV1
{
    Granted,
    Queued,
    OwnerBusy,
    StaleFence,
    NotFound,
    Terminal,
    Released
}
