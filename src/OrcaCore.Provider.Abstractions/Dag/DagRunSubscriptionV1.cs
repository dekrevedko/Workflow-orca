using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public sealed record DagRunSubscriptionV1(
    Guid SubscriptionId,
    Guid RecipientHostId,
    Guid RunId,
    StreamVersion AfterVersion,
    TimeSpan LeaseDuration);
