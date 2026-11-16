using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunLeaseResultV1(
    DagRunLeaseOutcomeV1 Outcome,
    DagRunFenceV1? Fence,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? HardDeadline,
    long? QueueSequence,
    StreamVersion? CurrentVersion);
