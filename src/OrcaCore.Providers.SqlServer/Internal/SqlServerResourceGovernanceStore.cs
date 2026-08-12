using System.Text.Json;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerResourceGovernanceStore(SqlServerStateDocumentStore documents)
    : IDurableResourceGovernanceStore
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
