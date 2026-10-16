using System.Text.Json;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerResourcePoolStore(SqlServerStateDocumentStore documents) : IResourcePoolStore
{
    public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.UpsertPoolAsync(definition, cancellationToken), cancellationToken);

    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.AcquireAsync(request, cancellationToken), cancellationToken);

    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ReleaseAsync(request, cancellationToken), cancellationToken);

    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
        string poolName,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.GetPoolAsync(poolName, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ListPoolsAsync(cancellationToken), cancellationToken);

    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        ExecuteAsync(engine => engine.ExpireTicketsAsync(now, cancellationToken), cancellationToken);

    public Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(engine => engine.GetReleaseEvidenceAsync(protectionToken, cancellationToken), cancellationToken);

    public Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(engine => engine.GetConfirmationBindingAsync(confirmationId, cancellationToken), cancellationToken);

    public Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(engine => engine.ConfirmAndReleaseAsync(request, cancellationToken), cancellationToken);

    public Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(engine => engine.PurgeReleaseEvidenceAsync(instanceId, cancellationToken), cancellationToken);

    private Task<TResult> ExecuteAsync<TResult>(
        Func<SqlServerResourcePoolStateEngine, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        documents.ExecuteAsync(
            SqlServerStateDocumentStore.ResourcePoolStateKey,
            Restore,
            operation,
            Capture,
            cancellationToken);

    private Task ExecuteAsync(
        Func<SqlServerResourcePoolStateEngine, Task> operation,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async engine =>
            {
                await operation(engine).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    private static SqlServerResourcePoolStateEngine Restore(string? payload) =>
        payload is null
            ? new SqlServerResourcePoolStateEngine()
            : new SqlServerResourcePoolStateEngine(
                JsonSerializer.Deserialize<SqlServerResourcePoolStateEngine.StateSnapshot>(
                    payload,
                    SqlServerStateDocumentStore.JsonOptions)
                ?? throw new InvalidOperationException("The SQL Server resource-pool state document was empty."));

    private static string Capture(SqlServerResourcePoolStateEngine engine) =>
        JsonSerializer.Serialize(engine.Capture(), SqlServerStateDocumentStore.JsonOptions);
}
