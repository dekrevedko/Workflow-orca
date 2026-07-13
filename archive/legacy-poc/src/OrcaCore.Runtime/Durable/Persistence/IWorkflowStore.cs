/// <summary>
/// Provider-facing durable persistence contract.
/// Application/runtime code should normally interact through <see cref="DurableWorkflowEngine"/>
/// and use <see cref="DurableArtifactRetentionPolicy"/> instead of raw cutoffs.
/// </summary>
namespace OrcaCore.Runtime.Durable.Persistence;

public interface IWorkflowStore
{
    Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct);

    Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct);

    Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct);

    Task<PersistedInstance> CommitAsync(WorkflowCommit commit, CancellationToken ct);

    Task<IReadOnlyList<PersistedInstance>> QueryAsync(
        WorkflowStatus? status = null,
        string? definitionId = null,
        string? definitionVersion = null,
        CancellationToken ct = default);

    Task<CorrelationLookupResult> LookupByCorrelationAsync(
        string eventName,
        string correlationId,
        CancellationToken ct);

    Task<IReadOnlyList<OutboxRecord>> LeaseDispatchableOutboxAsync(
        OutboxLeaseRequest request,
        CancellationToken ct);

    Task<OutboxRecord> CompleteLeasedOutboxAsync(
        string outboxId,
        string leaseOwner,
        DateTimeOffset dispatchedAt,
        CancellationToken ct);

    Task<OutboxRecord> FailLeasedOutboxAsync(
        string outboxId,
        string leaseOwner,
        OutboxDispatchFailure failure,
        CancellationToken ct);

    Task AppendHistoryAsync(string instanceId, HistoryRecord record, CancellationToken ct);

    // Low-level convenience overload for provider/operator scenarios.
    Task PurgeArtifactsAsync(string instanceId, DateTimeOffset olderThan, CancellationToken ct);

    Task PurgeArtifactsAsync(string instanceId, DurableArtifactRetentionCutoffs cutoffs, CancellationToken ct);

    Task DeleteAsync(string instanceId, CancellationToken ct);
}
