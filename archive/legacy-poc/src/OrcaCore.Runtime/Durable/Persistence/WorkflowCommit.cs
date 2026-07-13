namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record WorkflowCommit(
    PersistedInstance Instance,
    IReadOnlyList<InboxRecord> InboxRecords,
    IReadOnlyList<string> ProcessedInboxEventIds,
    IReadOnlyList<OutboxRecord> OutboxRecords,
    IReadOnlyList<ProjectionWorkItem> ProjectionWorkItems,
    IReadOnlyList<HistoryRecord> HistoryRecords);
