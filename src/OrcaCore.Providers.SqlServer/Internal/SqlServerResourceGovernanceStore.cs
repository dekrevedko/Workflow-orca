using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerResourceGovernanceStore(SqlServerStateDocumentStore documents)
    : ICheckpointGuardedResourceGovernanceStore
{
    public async ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            engine => engine.LoadAsync(partitionId, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);

    public async ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default) =>
        await ExecuteAsync(
            engine => engine.AppendAsync(partitionId, expectedVersion, records, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);

    public async ValueTask<ResourceGovernanceAppendResult> AppendIfCurrentAsync(
        ResourceGovernancePartitionId partitionId, long expectedVersion, IReadOnlyList<ResourceGovernanceRecord> records,
        IReadOnlyDictionary<InstanceId, long> checkpoints, CancellationToken cancellationToken)
    {
        var current = false;
        return await documents.ExecuteAsync<SqlServerResourceGovernanceStateEngine, ResourceGovernanceAppendResult>(
            SqlServerStateDocumentStore.ResourceGovernanceStateKey, Restore,
            engine => current ? engine.AppendAsync(partitionId, expectedVersion, records, cancellationToken).AsTask()
                : Task.FromResult<ResourceGovernanceAppendResult>(new ResourceGovernanceAppendResult.Conflict(expectedVersion)),
            Capture, cancellationToken, async (connection, transaction) =>
            {
                var payload = await documents.LoadPayloadAsync(connection, transaction,
                    SqlServerStateDocumentStore.WorkflowStateKey, cancellationToken).ConfigureAwait(false);
                if (payload is null) return;
                var state = SqlServerWorkflowStore.ReadSnapshot(payload);
                current = checkpoints.All(pair => state.Checkpoints.Any(checkpoint =>
                    checkpoint.InstanceId.Equals(pair.Key) && checkpoint.StreamVersion.Value == pair.Value));
            }).ConfigureAwait(false);
    }

    private Task<TResult> ExecuteAsync<TResult>(
        Func<SqlServerResourceGovernanceStateEngine, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        documents.ExecuteAsync(
            SqlServerStateDocumentStore.ResourceGovernanceStateKey,
            Restore,
            operation,
            Capture,
            cancellationToken);

    private static SqlServerResourceGovernanceStateEngine Restore(string? payload) =>
        payload is null
            ? new SqlServerResourceGovernanceStateEngine()
            : new SqlServerResourceGovernanceStateEngine(
                JsonSerializer.Deserialize<SqlServerResourceGovernanceStateEngine.StateSnapshot>(
                    payload,
                    SqlServerStateDocumentStore.JsonOptions)
                ?? throw new InvalidOperationException("The SQL Server resource-governance state document was empty."));

    private static string Capture(SqlServerResourceGovernanceStateEngine engine) =>
        JsonSerializer.Serialize(engine.Capture(), SqlServerStateDocumentStore.JsonOptions);
}
