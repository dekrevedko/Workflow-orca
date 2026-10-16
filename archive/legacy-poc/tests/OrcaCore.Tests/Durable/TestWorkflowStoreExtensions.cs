namespace OrcaCore.Tests.Durable;

internal static class TestWorkflowStoreExtensions
{
    public static Task<PersistedInstance> CreateAsync(
        this IWorkflowStore store,
        PersistedInstance data,
        CancellationToken ct) =>
        store.CreateAsync(new WorkflowCommit(data, [], [], [], [], []), ct);

    public static Task<PersistedInstance> CreateAsync(
        this IWorkflowStore store,
        PersistedInstance data,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct) =>
        store.CreateAsync(new WorkflowCommit(data, [], [], outboxRecords, [], historyRecords), ct);

    public static Task<PersistedInstance> CommitTransitionAsync(
        this IWorkflowStore store,
        PersistedInstance data,
        CancellationToken ct) =>
        store.CommitAsync(new WorkflowCommit(data, [], [], [], [], []), ct);

    public static Task<PersistedInstance> CommitTransitionAsync(
        this IWorkflowStore store,
        PersistedInstance data,
        IReadOnlyList<InboxRecord> inboxRecords,
        IReadOnlyList<string> processedInboxEventIds,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct) =>
        store.CommitAsync(
            new WorkflowCommit(data, inboxRecords, processedInboxEventIds, outboxRecords, [], historyRecords),
            ct);

    public static Task<IReadOnlyList<OutboxRecord>> GetPendingOutboxAsync(
        this InMemoryWorkflowStore store,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<OutboxRecord>>(
            store.GetOutboxRecords().Where(record => record.Status == OutboxStatus.Pending).ToArray());
    }
}
