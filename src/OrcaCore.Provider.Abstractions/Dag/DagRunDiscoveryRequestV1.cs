using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunDiscoveryRequestV1(long AfterCreationSequence, int MaxCount);
