using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunDiscoveryV1(
    long CreationSequence,
    Guid RunId,
    StreamVersion StreamVersion,
    DagRunBindingV1 Definition,
    DagRunStateV1 State);
