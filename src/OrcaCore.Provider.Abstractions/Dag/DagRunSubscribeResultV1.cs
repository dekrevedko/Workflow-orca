using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunSubscribeResultV1(
    DagRunSubscribeOutcomeV1 Outcome,
    StreamVersion? CurrentVersion);
