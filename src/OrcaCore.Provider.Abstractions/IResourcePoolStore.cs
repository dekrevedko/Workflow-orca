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
    /// Resizes a pool without revoking held tickets.
    /// </summary>
    Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken);

    /// <summary>
    /// Marks expired tickets as audibly expired without silently releasing them.
    /// </summary>
    Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

}
