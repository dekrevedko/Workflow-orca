using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Stores durable workflow event streams and checkpoints.
/// </summary>
public interface IWorkflowEventStore
{
    /// <summary>
    /// Loads the latest checkpoint for one workflow instance when a provider has one.
    /// </summary>
    Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends one accepted mutation with optimistic concurrency.
    /// </summary>
    Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads stream events after the supplied version.
    /// </summary>
    Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Stores durable inbound event delivery records.
/// </summary>
public interface IWorkflowInboxStore
{
    /// <summary>
    /// Gets an inbox record by event id.
    /// </summary>
    Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken);
}

/// <summary>
/// Stores durable outbound dispatch records.
/// </summary>
public interface IWorkflowOutboxStore
{
    /// <summary>
    /// Claims records eligible for dispatch.
    /// </summary>
    Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the dispatch state for one outbox record.
    /// </summary>
    Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks one outbox record with a dispatch state.
    /// </summary>
    Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken);
}

/// <summary>
/// Stores query and routing projections derived from workflow events.
/// </summary>
public interface IWorkflowProjectionStore
{
    /// <summary>
    /// Applies projection operations included in a commit batch.
    /// </summary>
    Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken);

    /// <summary>
    /// Lists projected instance summaries matching a structured query.
    /// </summary>
    Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts projected instance summaries matching a structured query.
    /// </summary>
    Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Lists projected active waits matching a structured query.
    /// </summary>
    Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets projected grouped statistics matching a structured query.
    /// </summary>
    Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken);
}

/// <summary>
/// Applies retention operations that must preserve active instances and dispatch safety.
/// </summary>
public interface IWorkflowRetentionStore
{
    /// <summary>
    /// Archives one inactive instance according to an explicit retention policy.
    /// </summary>
    Task<ArchiveResult> ArchiveAsync(RetentionPolicy policy, CancellationToken cancellationToken);

    /// <summary>
    /// Purges retained data according to an explicit retention policy when no dispatch is in flight.
    /// </summary>
    Task<PurgeResult> PurgeAsync(RetentionPolicy policy, CancellationToken cancellationToken);
}

/// <summary>
/// Schedules durable timer wake-up commands.
/// </summary>
public interface ITimerScheduler
{
    /// <summary>
    /// Schedules a durable wake-up.
    /// </summary>
    Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Claims due wake-ups and returns timer-fire commands exactly once.
    /// </summary>
    Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches normalized outbox records to an external transport.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatches one outbox record.
    /// </summary>
    Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken);
}

/// <summary>
/// Serializes payloads crossing durable provider boundaries.
/// </summary>
public interface IWorkflowPayloadSerializer
{
    /// <summary>
    /// Serializes a payload to bytes and content metadata.
    /// </summary>
    SerializedPayload Serialize<TPayload>(TPayload payload);

    /// <summary>
    /// Deserializes a payload from bytes and content metadata.
    /// </summary>
    TPayload Deserialize<TPayload>(SerializedPayload payload);
}

/// <summary>
/// Describes a durable timer schedule request.
/// </summary>
public sealed record TimerScheduleRequest
{
    /// <summary>
    /// Gets the timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }

    /// <summary>
    /// Gets the target workflow instance.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the command identity to use when the timer fires.
    /// </summary>
    public required CommandId CommandId { get; init; }

    /// <summary>
    /// Gets when the wake-up becomes due.
    /// </summary>
    public required DateTimeOffset FireAt { get; init; }

    /// <summary>
    /// Gets the logical wake-up name.
    /// </summary>
    public required string WakeupName { get; init; }
}

/// <summary>
/// Describes one serialized payload.
/// </summary>
public sealed record SerializedPayload(string ContentType, byte[] Payload);

/// <summary>
/// Describes dispatch outcome categories.
/// </summary>
public enum DispatchResult
{
    /// <summary>
    /// The record was dispatched successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The dispatch failed and can be retried.
    /// </summary>
    RetryableFailure,

    /// <summary>
    /// The dispatch failed permanently.
    /// </summary>
    PermanentFailure
}
