/// <summary>
/// Provider-facing durable persistence contract.
/// Application/runtime code should normally interact through <see cref="DurableWorkflowEngine"/>
/// and use <see cref="DurableArtifactRetentionPolicy"/> instead of raw cutoffs.
/// </summary>
namespace OrcaCore.Runtime.Durable.Persistence;

public interface IWorkflowStore
{
    Task CreateAsync(PersistedInstance data, CancellationToken ct);

    Task CreateAsync(
        PersistedInstance data,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct);

    Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct);

    Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct);

    Task<PersistedInstance> CommitTransitionAsync(PersistedInstance data, CancellationToken ct);

    Task<PersistedInstance> CommitTransitionAsync(
        PersistedInstance data,
        IReadOnlyList<InboxRecord> inboxRecords,
        IReadOnlyList<string> processedInboxEventIds,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct);

    Task<IReadOnlyList<PersistedInstance>> QueryAsync(
        WorkflowStatus? status = null,
        string? definitionId = null,
        string? definitionVersion = null,
        CancellationToken ct = default);

    Task<CorrelationLookupResult> LookupByCorrelationAsync(
        string eventName,
        string correlationId,
        CancellationToken ct);

    Task<IReadOnlyList<OutboxRecord>> GetPendingOutboxAsync(CancellationToken ct);

    Task MarkOutboxDispatchedAsync(string outboxId, CancellationToken ct);

    Task<OutboxRecord> RecordOutboxDispatchFailureAsync(
        string outboxId,
        string? error,
        DateTimeOffset failedAt,
        bool poison,
        CancellationToken ct);

    Task AppendHistoryAsync(string instanceId, HistoryRecord record, CancellationToken ct);

    // Low-level convenience overload for provider/operator scenarios.
    Task PurgeArtifactsAsync(string instanceId, DateTimeOffset olderThan, CancellationToken ct);

    Task PurgeArtifactsAsync(string instanceId, DurableArtifactRetentionCutoffs cutoffs, CancellationToken ct);

    Task DeleteAsync(string instanceId, CancellationToken ct);
}
