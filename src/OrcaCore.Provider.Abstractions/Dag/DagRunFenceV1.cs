using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunFenceV1(Guid RunId, Guid OwnerId, long Token, Guid RequestId);
