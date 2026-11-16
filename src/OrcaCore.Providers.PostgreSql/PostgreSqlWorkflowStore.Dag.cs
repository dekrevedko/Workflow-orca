using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Providers.Dag.Internal;
using Npgsql;

namespace OrcaCore.Providers.PostgreSql;

internal sealed partial class PostgreSqlWorkflowStore
{
    internal DagTransactionClock DagTransactionClock { get; } = new();

    private DagRunSqlStore DagStore => new(() => dataSource.CreateConnection(), false, "", DagTransactionClock,
        exception => exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation });

    public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
        WorkflowStreamId streamId, CancellationToken cancellationToken) =>
        DagStore.LoadCheckpointAsync(streamId, cancellationToken);

    public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
        DagRunStartKeyV1 startKey, CancellationToken cancellationToken) =>
        DagStore.GetByStartKeyAsync(startKey, cancellationToken);

    public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
        WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken) =>
        DagStore.LoadTailAsync(streamId, afterVersion, cancellationToken);

    public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
        DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
        DagStore.AcquireAsync(request, cancellationToken);

    public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
        DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
        DagStore.RenewAsync(request, cancellationToken);

    public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
        DagRunFenceV1 fence, CancellationToken cancellationToken) =>
        DagStore.ReleaseAsync(fence, cancellationToken);

    public Task CancelDagRunLeaseRequestAsync(
        Guid runId, Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
        DagStore.CancelRequestAsync(runId, ownerId, requestId, cancellationToken);

    public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
        DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken) =>
        DagStore.DiscoverAsync(request, cancellationToken);

    public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
        DagRunSubscriptionV1 subscription, CancellationToken cancellationToken) =>
        DagStore.SubscribeAsync(subscription, cancellationToken);

    public Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
        CancellationToken cancellationToken) =>
        DagStore.UnsubscribeAsync(subscriptionId, recipientHostId, cancellationToken);

    public Task<int> PruneExpiredDagRunObserversAsync(
        int maxCount, CancellationToken cancellationToken) =>
        DagStore.PruneObserversAsync(maxCount, cancellationToken);
}
