using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Management;

/// <summary>
/// Provides the root management surface for durable workflow projections.
/// </summary>
public sealed class DurableManagement(
    IWorkflowProjectionStore projectionStore,
    IResourcePoolStore? resourcePoolStore = null,
    IWorkflowEventStore? eventStore = null,
    DurableCommandProcessor? commandProcessor = null)
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
        ArgumentNullException.ThrowIfNull(definitionId);
        return All().Where(instance => instance.DefinitionId.Equals(definitionId));
    }

    /// <summary>
    /// Selects one durable instance by id.
    /// </summary>
    public DurableManagementQuery Instance(InstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        return All().Where(instance => instance.InstanceId.Equals(instanceId));
    }

    /// <summary>
    /// Pauses one durable instance at the next safe boundary.
    /// </summary>
    internal Task<DurableCommandResult> PauseAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        return RequiredCommandProcessor().ProcessAsync(
            new DurablePauseCommand(CommandId.New(), instanceId, requestedAt),
            cancellationToken);
    }

    /// <summary>
    /// Resumes one paused durable instance.
    /// </summary>
    internal Task<DurableCommandResult> ResumeAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        bool replayBufferedDeliveries,
        CancellationToken cancellationToken)
    {
        return RequiredCommandProcessor().ProcessAsync(
            new DurableResumeCommand(
                CommandId.New(),
                instanceId,
                requestedAt,
                replayBufferedDeliveries ? ResumeBufferedDeliveries.Replay : ResumeBufferedDeliveries.Discard),
            cancellationToken);
    }

    /// <summary>
    /// Cancels one durable instance.
    /// </summary>
    public Task<DurableCommandResult> CancelAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        return RequiredCommandProcessor().ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = requestedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Terminates one durable instance.
    /// </summary>
    public Task<DurableCommandResult> TerminateAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        throw new WorkflowLifecycleException(
            "Durable Terminate requires explicit destructive safety confirmation.");
    }

    /// <summary>
    /// Terminates one durable instance after explicit destructive safety confirmation.
    /// </summary>
    public Task<DurableCommandResult> TerminateAsync(
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        DestructiveCommandSafety safety,
        CancellationToken cancellationToken)
    {
        RequireDestructiveSafety(safety, "Terminate");
        return RequiredCommandProcessor().ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = requestedAt
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads the committed durable event history for one instance.
    /// </summary>
    public Task<IReadOnlyList<DurableWorkflowEvent>> GetHistoryAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return RequiredWorkflowEventStore()
            .LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, cancellationToken);
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
    /// Archives inactive durable metadata according to an explicit retention policy.
    /// </summary>
    internal Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        return RequiredRetentionStore().ArchiveAsync(policy, cancellationToken);
    }

    /// <summary>
    /// Purges retained durable data according to an explicit retention policy.
    /// </summary>
    internal Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken)
    {
        throw new WorkflowLifecycleException(
            "Durable Purge requires explicit destructive safety confirmation.");
    }

    /// <summary>
    /// Purges retained durable data according to an explicit retention policy after destructive safety confirmation.
    /// </summary>
    internal Task<PurgeResult> PurgeAsync(
        RetentionPolicy policy,
        DestructiveCommandSafety safety,
        CancellationToken cancellationToken)
    {
        RequireDestructiveSafety(safety, "Purge");
        return RequiredRetentionStore().PurgeAsync(policy, cancellationToken);
    }

    /// <summary>
    /// Reconstructs DAG node status from durable child materialization history and instance projections.
    /// </summary>
    public async Task<DagRunSnapshot> ReconstructDagRunAsync(
        InstanceId rootInstanceId,
        CancellationToken cancellationToken)
    {
        var eventStore = RequiredWorkflowEventStore();
        var checkpoint = await eventStore
            .LoadCheckpointAsync(rootInstanceId, cancellationToken)
            .ConfigureAwait(false);
        var afterVersion = checkpoint.HasValue
            ? checkpoint.Value.StreamVersion
            : StreamVersion.Empty;
        var rootEvents = await eventStore
            .LoadTailAsync(new WorkflowStreamId(rootInstanceId), afterVersion, cancellationToken)
            .ConfigureAwait(false);
        var childNodes = new List<DagNodeDraft>();
        if (checkpoint.HasValue)
        {
            childNodes.AddRange(ChildNodesFromCheckpoint(checkpoint.Value));
        }

        childNodes.AddRange(rootEvents.SelectMany(ChildNodesFromEvent));
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

    private static IEnumerable<DagNodeDraft> ChildNodesFromCheckpoint(CheckpointWrite checkpoint)
    {
        foreach (var child in checkpoint.RuntimeState.ActiveChildren)
        {
            yield return new DagNodeDraft(
                NodeId(child.ItemSnapshot, child.ChildInstanceId),
                child.ChildInstanceId,
                DateTimeOffset.MinValue);
        }

        foreach (var group in checkpoint.RuntimeState.ActiveChildGroups)
        {
            foreach (var child in group.Children)
            {
                yield return new DagNodeDraft(
                    NodeId(child.ItemSnapshot, child.ChildInstanceId),
                    child.ChildInstanceId,
                    DateTimeOffset.MinValue);
            }
        }
    }

    private static IEnumerable<DagNodeDraft> ChildNodesFromEvent(DurableWorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowChildrenScheduledEvent scheduled => scheduled.Children.Select(child =>
                new DagNodeDraft(
                    NodeId(child.ItemSnapshot, child.ChildInstanceId),
                    child.ChildInstanceId,
                    scheduled.OccurredAt)),
            WorkflowChildrenDispatchedEvent dispatched => dispatched.Children.Select(child =>
                new DagNodeDraft(
                    NodeId(child.ItemSnapshot, child.ChildInstanceId),
                    child.ChildInstanceId,
                    dispatched.OccurredAt)),
            _ => []
        };
    }

    private static string NodeId(string? itemSnapshot, InstanceId childInstanceId)
    {
        return string.IsNullOrWhiteSpace(itemSnapshot)
            ? childInstanceId.Value.ToString("N")
            : itemSnapshot;
    }

    private static void RequireDestructiveSafety(DestructiveCommandSafety safety, string operation)
    {
        if (safety != DestructiveCommandSafety.Confirmed)
        {
            throw new WorkflowLifecycleException(
                $"Durable {operation} requires explicit destructive safety confirmation.");
        }
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

    private DurableCommandProcessor RequiredCommandProcessor()
    {
        return commandProcessor ?? new DurableCommandProcessor(RequiredWorkflowEventStore(), resourcePoolStore);
    }

    private IWorkflowRetentionStore RequiredRetentionStore()
    {
        return retentionStore ?? throw new InvalidOperationException(
            "Durable retention management requires a retention-capable provider.");
    }
}

/// <summary>
/// Explicit confirmation token for durable destructive management commands.
/// </summary>
public enum DestructiveCommandSafety
{
    /// <summary>
    /// Caller explicitly confirmed the destructive breadth and host-side authorization requirements.
    /// </summary>
    Confirmed
}

internal sealed record DagNodeDraft(
    string NodeId,
    InstanceId ChildInstanceId,
    DateTimeOffset StartedAt);

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
