using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Providers.Dag.Internal;
using Microsoft.Data.SqlClient;
using System.Data.Common;
using System.Text.Json;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed partial class SqlServerWorkflowStore
{
    internal DagTransactionClock DagTransactionClock { get; } = new();

    private async Task<Result<AppendEventsResult>> AppendWorkflowAsync(ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        try
        {
            return await documents.ExecuteAsync(SqlServerStateDocumentStore.WorkflowStateKey, Restore,
                engine => engine.AppendAsync(batch, cancellationToken), Capture, cancellationToken,
                async (connection, transaction) =>
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "select 1 from " + SqlServerSchema.QuoteSchema(documents.Schema) +
                                          ".[dag_runs] where run_id=@id";
                    command.Parameters.Add(new SqlParameter("@id", batch.StreamId.InstanceId.Value));
                    if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                        throw new DagAggregateCollisionException();
                }).ConfigureAwait(false);
        }
        catch (DagAggregateCollisionException)
        {
            return DagRunStoreRules.Failure("DAG_INTEGRITY_CONFLICT", "DAG run owns this aggregate identity");
        }
    }

    private sealed class DagAggregateCollisionException : Exception;

    private async Task<WorkflowOperatorStatistics> ReadDagAndWorkflowStatisticsAsync(CancellationToken token)
    {
        await documents.InitializeAsync(token).ConfigureAwait(false);
        await using var connection = new SqlConnection(documents.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, token).ConfigureAwait(false);
        await SqlServerSchema.AcquireTransactionLockAsync(connection, transaction,
            "orcacore:state:" + documents.Schema + ":workflow", token).ConfigureAwait(false);
        var engine = Restore(await documents
            .LoadPayloadAsync(connection, transaction, SqlServerStateDocumentStore.WorkflowStateKey, token)
            .ConfigureAwait(false));
        var statistics = await engine.GetOperatorStatisticsAsync(token).ConfigureAwait(false);
        var pressure = await DagStore.ReadPressureAsync(connection, transaction, statistics.Pressure, token)
            .ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return statistics with { Pressure = pressure };
    }

    private DagRunSqlStore DagStore => new(() => new SqlConnection(documents.ConnectionString), true,
        SqlServerSchema.QuoteSchema(documents.Schema) + ".", DagTransactionClock,
        exception => exception is SqlException { Number: 2601 or 2627 }, documents.InitializeAsync,
        async (connection, transaction, token) =>
        {
            await SqlServerSchema.AcquireTransactionLockAsync((SqlConnection)connection, (SqlTransaction)transaction,
                "orcacore:state:" + documents.Schema + ":workflow", token).ConfigureAwait(false);
            return new DagWorkflowTransaction(documents, (SqlConnection)connection, (SqlTransaction)transaction,
                Restore);
        });

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

    private sealed class DagWorkflowTransaction(
        SqlServerStateDocumentStore owner,
        SqlConnection connection,
        SqlTransaction transaction,
        Func<string?, SqlServerWorkflowStateEngine> restore) : IDagRunWorkflowTransaction
    {
        private SqlServerWorkflowStateEngine? state;

        private async Task<SqlServerWorkflowStateEngine> ReadAsync(CancellationToken cancellationToken)
        {
            if (state is not null) return state;
            state = restore(await owner.LoadPayloadAsync(connection, transaction,
                SqlServerStateDocumentStore.WorkflowStateKey, cancellationToken).ConfigureAwait(false));
            return state;
        }

        public async Task<bool> WorkflowExistsAsync(Guid id, CancellationToken cancellationToken) =>
            (await ReadAsync(cancellationToken).ConfigureAwait(false)).ContainsWorkflow(id);

        public async Task AddOutboxAsync(Guid runId,
            IReadOnlyList<global::OrcaCore.Abstractions.Providers.OutboxWrite> writes,
            CancellationToken cancellationToken)
        {
            var engine = await ReadAsync(cancellationToken).ConfigureAwait(false);
            engine.AddDagOutbox(runId, writes);
            await owner.SavePayloadAsync(connection, transaction, SqlServerStateDocumentStore.WorkflowStateKey,
                Capture(engine), cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> PruneObserverAsync(DagRunObserverRow observer, DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var engine = await ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!engine.PruneDagObserver(observer.Subscription.RunId, observer.Subscription.SubscriptionId,
                    observer.Subscription.RecipientHostId, now)) return false;
            await owner.SavePayloadAsync(connection, transaction, SqlServerStateDocumentStore.WorkflowStateKey,
                Capture(engine), cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
