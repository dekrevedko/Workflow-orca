using System.Text.Json;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerResourceGovernanceStore(SqlServerStateDocumentStore documents)
    : IDurableResourceGovernanceStore
{
    public async ValueTask<ResourceGovernanceHead> LoadLatestAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        var payload = await documents.ReadPayloadAsync(SqlServerStateDocumentStore.ResourceGovernanceStateKey, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null) return ResourceGovernanceHead.Create(partitionId, 0, null);

        // The experimental adapter stores all partitions in a single JSON document.
        // Parse the container but materialize only the requested partition's last record.
        using var document = JsonDocument.Parse(payload);
        var head = ResourceGovernanceHead.Create(partitionId, 0, null);
        var found = false;
        foreach (var stream in document.RootElement.GetProperty("streams").EnumerateArray())
        {
            if (!string.Equals(stream.GetProperty("partitionId").GetString(), partitionId.Value, StringComparison.Ordinal))
                continue;
            if (found) throw new InvalidDataException($"Duplicate governance partition '{partitionId}'.");
            found = true;
            var records = stream.GetProperty("records");
            var count = records.GetArrayLength();
            // Documents written before the explicit version field used the atomic list length as the head.
            var version = stream.TryGetProperty("version", out var storedVersion) ? storedVersion.GetInt64() : count;
            if (version != count)
                throw new InvalidDataException($"Governance partition '{partitionId}' declares version {version} with {count} records.");
            var record = count == 0 ? null : records[count - 1].Deserialize<ResourceGovernanceRecord>(SqlServerStateDocumentStore.JsonOptions);
            head = ResourceGovernanceHead.Create(partitionId, version, record);
        }

        return head;
    }

    public async ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default) =>
        await documents.ReadAsync(
            SqlServerStateDocumentStore.ResourceGovernanceStateKey, Restore,
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
