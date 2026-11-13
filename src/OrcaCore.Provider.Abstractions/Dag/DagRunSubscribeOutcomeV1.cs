using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers.Dag;

public enum DagRunSubscribeOutcomeV1
{
    Subscribed,
    AlreadyChanged,
    NotFound
}
