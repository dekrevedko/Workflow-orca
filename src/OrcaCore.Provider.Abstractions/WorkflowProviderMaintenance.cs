using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

/// <summary>Describes a provider-owned maintenance operation for one durable workflow instance.</summary>
public sealed record WorkflowProviderMaintenanceRequest(
    InstanceId InstanceId,
    DateTimeOffset RequestedAt);

/// <summary>Describes the outcome of a provider-owned maintenance operation.</summary>
public enum WorkflowProviderMaintenanceDisposition
{
    NotFound,
    Rejected,
    Archived,
    Purged
}

/// <summary>Describes the live reference that prevents physical cleanup.</summary>
public enum WorkflowProviderMaintenanceBlocker
{
    ActiveInstance,
    PendingInboxDelivery,
    PendingOutboxDispatch,
    ClaimedOutboxDispatch,
    PoisonedOutboxDispatch
}

/// <summary>Provides a provider-owned maintenance result without reintroducing application purge vocabulary.</summary>
public sealed record WorkflowProviderMaintenanceResult(
    WorkflowProviderMaintenanceDisposition Disposition,
    WorkflowProviderMaintenanceBlocker? Blocker = null);

/// <summary>Provides a provider-authoritative maintenance inspection before an operation.</summary>
public sealed record WorkflowProviderMaintenanceInspection(
    bool Exists,
    DateTimeOffset? ArchivedAt,
    WorkflowProviderMaintenanceBlocker? Blocker);

/// <summary>
/// Provides the production provider maintenance owner for archival and reference-safe physical cleanup.
/// Confirmation records and monotonic route revisions are retained as tombstones.
/// </summary>
public interface IWorkflowProviderMaintenanceStore
{
    Task<WorkflowProviderMaintenanceInspection> InspectForMaintenanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    Task<WorkflowProviderMaintenanceResult> ArchiveForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken);

    Task<WorkflowProviderMaintenanceResult> PurgeForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken);
}
