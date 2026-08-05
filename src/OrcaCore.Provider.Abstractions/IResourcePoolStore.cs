using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Stores durable resource pools, tickets, and FIFO acquisition waiters.
/// </summary>
public interface IResourcePoolStore
{
    /// <summary>
    /// Creates or updates a named pool.
    /// </summary>
    Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to acquire all requested pool tickets atomically.
    /// </summary>
    Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Releases all tickets held by one guarded workflow holder.
    /// </summary>
    Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one pool snapshot.
    /// </summary>
    Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all pool snapshots visible to this store for telemetry and management summaries.
    /// </summary>
    Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<ResourcePoolSnapshot>>([]);
    }

    /// <summary>
    /// Marks expired tickets as audibly expired without silently releasing them.
    /// </summary>
    Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Gets retained causal release evidence for one exact protection token.</summary>
    Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Option<ResourcePoolReleaseEvidence>.None);

    /// <summary>Gets the token bound to one accepted stop-confirmation identity.</summary>
    Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Option<LeaseProtectionToken>.None);

    /// <summary>Atomically binds a trusted confirmation and releases its exact holder.</summary>
    async Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var released = await ReleaseAsync(
            new ResourcePoolReleaseRequest(
                request.HolderInstanceId,
                request.HolderKey,
                request.ConfirmedAt),
            cancellationToken).ConfigureAwait(false);
        return released.ReleasedTickets.Count == 0
            ? ResourcePoolStopConfirmationStatus.TokenNotFound
            : ResourcePoolStopConfirmationStatus.Released;
    }

    /// <summary>Purges retained release evidence after the provider deduplication window.</summary>
    Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

}
