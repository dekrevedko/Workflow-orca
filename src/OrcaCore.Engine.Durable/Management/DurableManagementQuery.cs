using System.Linq.Expressions;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Management;

/// <summary>
/// Represents one composable durable management projection query.
/// </summary>
public sealed class DurableManagementQuery(
    IWorkflowProjectionStore projectionStore,
    WorkflowProjectionQuery query)
{
    /// <summary>
    /// Adds a constrained metadata predicate to the current selection.
    /// </summary>
    public DurableManagementQuery Where(Expression<Func<WorkflowInstanceQueryModel, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return new DurableManagementQuery(projectionStore, ProjectionPredicateTranslator.Translate(query, predicate));
    }

    /// <summary>
    /// Lists projected instance snapshots in the current selection.
    /// </summary>
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        return projectionStore.ListAsync(query, cancellationToken);
    }

    /// <summary>
    /// Counts projected instance snapshots in the current selection.
    /// </summary>
    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        return projectionStore.CountAsync(query, cancellationToken);
    }

    /// <summary>
    /// Gets the single projected snapshot in the current selection.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(cancellationToken).ConfigureAwait(false);
        return snapshots.Single();
    }

    /// <summary>
    /// Gets projected active waits for instances in the current selection.
    /// </summary>
    public Task<IReadOnlyList<ActiveWaitSnapshot>> GetActiveWaitsAsync(CancellationToken cancellationToken)
    {
        return projectionStore.ListActiveWaitsAsync(query, cancellationToken);
    }

    /// <summary>
    /// Returns grouped projected status statistics for instances in the current selection.
    /// </summary>
    public Task<WorkflowStatistics> StatisticsAsync(CancellationToken cancellationToken)
    {
        return projectionStore.GetStatisticsAsync(query, cancellationToken);
    }
}
