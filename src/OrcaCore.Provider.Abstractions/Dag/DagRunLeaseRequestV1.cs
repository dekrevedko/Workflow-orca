using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunLeaseRequestV1(
    Guid RunId,
    Guid OwnerId,
    Guid RequestId,
    DagRunLeasePurposeV1 Purpose,
    long? ExpectedFence,
    TimeSpan LeaseDuration);
