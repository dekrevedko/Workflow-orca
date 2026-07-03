using StackExchange.Redis;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.Redis;

/// <summary>
/// Provides Redis-profile projection/cache behavior without implementing durable event-store semantics.
/// </summary>
public sealed class RedisProjectionStore : IWorkflowProjectionStore
{
    private readonly object gate = new();
    private readonly Dictionary<InstanceId, WorkflowInstanceSnapshot> summaries = [];
    private readonly IDatabase? redisDatabase;

    /// <summary>
    /// Initializes a projection store using the in-process cache profile.
    /// </summary>
    public RedisProjectionStore()
    {
    }

    /// <summary>
    /// Initializes a projection store bound to a StackExchange.Redis database adapter.
    /// </summary>
    public RedisProjectionStore(IDatabase redisDatabase)
    {
        ArgumentNullException.ThrowIfNull(redisDatabase);
        this.redisDatabase = redisDatabase;
    }

    /// <summary>
    /// Gets whether this instance was created with a Redis database adapter.
    /// </summary>
    public bool UsesRedisAdapter => redisDatabase is not null;

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            foreach (var operation in operations)
            {
                if (operation.InstanceSnapshot is { } snapshot)
                {
                    summaries[operation.InstanceId] = CloneSnapshot(snapshot);
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>(
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(query, cancellationToken).ConfigureAwait(false);
        return snapshots.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(query, cancellationToken).ConfigureAwait(false);
        return snapshots
            .SelectMany(snapshot => snapshot.ActiveWaits)
            .Select(wait => wait with { })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(query, cancellationToken).ConfigureAwait(false);
        return new WorkflowStatistics
        {
            Groups = snapshots
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray()
        };
    }

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            (query.ActiveWaitEventName is null || snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal))) &&
            (query.ActiveWaitCorrelationId is null || snapshot.ActiveWaits.Any(wait =>
                wait.CorrelationId == query.ActiveWaitCorrelationId));
    }

    private static WorkflowInstanceSnapshot CloneSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(wait => wait with { }).ToArray(),
            SagaAudits = snapshot.SagaAudits.Select(scope => scope with
            {
                ForwardActions = scope.ForwardActions.Select(action => action with { }).ToArray(),
                CompensationActions = scope.CompensationActions.Select(action => action with { }).ToArray(),
                RecoveryInterventions = scope.RecoveryInterventions.Select(intervention => intervention with { }).ToArray()
            }).ToArray()
        };
    }
}
