using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Management;

/// <summary>
/// Provides the root management surface for durable workflow projections.
/// </summary>
public sealed class DurableManagement(
    IWorkflowProjectionStore projectionStore,
    IResourcePoolStore? resourcePoolStore = null,
    IWorkflowEventStore? eventStore = null)
{
    private readonly IWorkflowEventStore? eventStore = eventStore ?? projectionStore as IWorkflowEventStore;
    private readonly IWorkflowRetentionStore? retentionStore = projectionStore as IWorkflowRetentionStore;

    /// <summary>
    /// Selects all durable instances visible in projections.
    /// </summary>
    public DurableManagementQuery All()
    {
        return new DurableManagementQuery(projectionStore, WorkflowProjectionQuery.All);
    }

    /// <summary>
    /// Selects durable instances for one definition.
    /// </summary>
    public DurableManagementQuery ForDefinition(DefinitionId definitionId)
    {
        return All().Where(instance => instance.DefinitionId == definitionId);
    }

    /// <summary>
    /// Selects one durable instance by id.
    /// </summary>
    public DurableManagementQuery Instance(InstanceId instanceId)
    {
        return All().Where(instance => instance.InstanceId == instanceId);
    }

    /// <summary>
    /// Gets one durable resource-pool snapshot.
    /// </summary>
    public async Task<ResourcePoolSnapshot> GetResourcePoolAsync(
        string poolName,
        CancellationToken cancellationToken)
    {
        var snapshot = await RequiredResourcePoolStore()
            .GetPoolAsync(poolName, cancellationToken)
            .ConfigureAwait(false);
        return snapshot.Value;
    }

    /// <summary>
    /// Gets durable saga audit state for one instance from projections.
    /// </summary>
    public async Task<SagaAuditSnapshot> GetSagaAuditAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var snapshots = await projectionStore
            .ListAsync(new WorkflowProjectionQuery { InstanceId = instanceId }, cancellationToken)
            .ConfigureAwait(false);
        var snapshot = snapshots.SingleOrDefault();
        return new SagaAuditSnapshot
        {
            Scopes = snapshot?.SagaAudits ?? []
        };
    }

    /// <summary>
    /// Resizes a durable resource pool without revoking held tickets.
    /// </summary>
    public Task ResizeResourcePoolAsync(
        string poolName,
        int capacity,
        CancellationToken cancellationToken)
    {
        return RequiredResourcePoolStore().ResizePoolAsync(poolName, capacity, cancellationToken);
    }

    /// <summary>
    /// Scans for expired tickets and records audible expiry state.
    /// </summary>
    public Task<ResourcePoolExpiryResult> ExpireResourcePoolTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return RequiredResourcePoolStore().ExpireTicketsAsync(now, cancellationToken);
    }

    /// <summary>
    /// Force-releases one ticket as an audited operator action.
    /// </summary>
    public Task<ResourcePoolForceReleaseResult> ForceReleaseResourcePoolTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        return RequiredResourcePoolStore()
            .ForceReleaseTicketAsync(ticketId, reason, releasedAt, cancellationToken);
    }

    /// <summary>
    /// Archives inactive durable metadata according to an explicit retention policy.
    /// </summary>
    public Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return RequiredRetentionStore().ArchiveAsync(policy, cancellationToken);
    }

    /// <summary>
    /// Purges retained durable data according to an explicit retention policy.
    /// </summary>
    public Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return RequiredRetentionStore().PurgeAsync(policy, cancellationToken);
    }

    /// <summary>
    /// Reconstructs DAG node status from durable child materialization history and instance projections.
    /// </summary>
    public async Task<DagRunSnapshot> ReconstructDagRunAsync(
        InstanceId rootInstanceId,
        CancellationToken cancellationToken)
    {
        var rootEvents = await RequiredWorkflowEventStore()
            .LoadTailAsync(new WorkflowStreamId(rootInstanceId), StreamVersion.Empty, cancellationToken)
            .ConfigureAwait(false);
        var childNodes = rootEvents
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Select(child => new
            {
                NodeId = child.ItemSnapshot,
                child.ChildInstanceId,
                StartedAt = group.OccurredAt
            }))
            .ToArray();
        var childProjections = await projectionStore
            .ListAsync(new WorkflowProjectionQuery { RootInstanceId = rootInstanceId }, cancellationToken)
            .ConfigureAwait(false);
        var projectionsById = childProjections.ToDictionary(snapshot => snapshot.InstanceId);
        var nodes = childNodes
            .Select(child =>
            {
                projectionsById.TryGetValue(child.ChildInstanceId, out var projection);
                return new DagNodeRunSnapshot(
                    child.NodeId,
                    child.ChildInstanceId,
                    projection?.Status ?? WorkflowStatus.Running,
                    projection?.CreatedAt ?? child.StartedAt,
                    projection?.UpdatedAt ?? child.StartedAt,
                    projection?.ErrorSummary);
            })
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();

        return new DagRunSnapshot(rootInstanceId, nodes);
    }

    private IResourcePoolStore RequiredResourcePoolStore()
    {
        return resourcePoolStore ?? throw new InvalidOperationException(
            "Durable resource-pool management requires a resource-pool-capable provider.");
    }

    private IWorkflowEventStore RequiredWorkflowEventStore()
    {
        return eventStore ?? throw new InvalidOperationException(
            "DAG run reconstruction requires an event-store-capable provider.");
    }

    private IWorkflowRetentionStore RequiredRetentionStore()
    {
        return retentionStore ?? throw new InvalidOperationException(
            "Durable retention management requires a retention-capable provider.");
    }
}

/// <summary>
/// Immutable reconstruction of a DAG run from durable workflow metadata.
/// </summary>
public sealed record DagRunSnapshot(
    InstanceId RootInstanceId,
    IReadOnlyList<DagNodeRunSnapshot> Nodes);

/// <summary>
/// Immutable reconstruction of one DAG node instance.
/// </summary>
public sealed record DagNodeRunSnapshot(
    string NodeId,
    InstanceId InstanceId,
    WorkflowStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string? ErrorSummary);
