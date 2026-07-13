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
/// Stores durable start idempotency mappings.
/// </summary>
public interface IWorkflowStartIdempotencyStore
{
    /// <summary>
    /// Gets the instance bound to one start idempotency key, when present.
    /// </summary>
    Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes the workflow instance that won a durable start idempotency key.
/// </summary>
public sealed record StartedWorkflowIdempotencyRecord(
    string IdempotencyKey,
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion);

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
    /// Claims records eligible for dispatch with a recoverable lease.
    /// </summary>
    Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken);

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

    /// <summary>
    /// Releases one claimed outbox record so it can be retried.
    /// </summary>
    Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken);
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

    /// <summary>
    /// Claims due wake-ups with a recoverable lease and returns timer-fire commands.
    /// </summary>
    Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes one claimed timer after its fire command has been durably processed.
    /// </summary>
    Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken);

    /// <summary>
    /// Releases one claimed timer so it can be retried.
    /// </summary>
    Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken);
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
/// Describes a recoverable durable outbox claim request. The optional kind selector partitions
/// the outbox so disjoint pumps consume disjoint record kinds (DR-037): the external message
/// dispatcher excludes the internal <c>continue</c> kind, and the continuation pump includes
/// only it. When no selector is set, all kinds are claimable (backward-compatible default).
/// </summary>
public sealed record OutboxClaimRequest(
    int MaxCount,
    DateTimeOffset ClaimedAt,
    TimeSpan LeaseDuration)
{
    /// <summary>
    /// Gets the kind selector. Null claims any kind; an include set claims only listed kinds;
    /// an exclude set claims any kind except the listed ones.
    /// </summary>
    public OutboxKindSelector? KindSelector { get; init; }
}

/// <summary>
/// Selects outbox record kinds for a partitioned claim (DR-037). Exactly one of
/// <see cref="Include"/> or <see cref="Exclude"/> is set.
/// </summary>
public sealed record OutboxKindSelector
{
    private OutboxKindSelector(IReadOnlySet<string>? include, IReadOnlySet<string>? exclude)
    {
        Include = include;
        Exclude = exclude;
    }

    /// <summary>
    /// Gets the kinds to claim exclusively, when this is an include selector.
    /// </summary>
    public IReadOnlySet<string>? Include { get; }

    /// <summary>
    /// Gets the kinds to skip, when this is an exclude selector.
    /// </summary>
    public IReadOnlySet<string>? Exclude { get; }

    /// <summary>
    /// Creates a selector that claims only the listed kinds.
    /// </summary>
    public static OutboxKindSelector Including(params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        return new OutboxKindSelector(new HashSet<string>(kinds, StringComparer.Ordinal), null);
    }

    /// <summary>
    /// Creates a selector that claims any kind except the listed ones.
    /// </summary>
    public static OutboxKindSelector Excluding(params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        return new OutboxKindSelector(null, new HashSet<string>(kinds, StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns whether a record of the given kind matches this selector.
    /// </summary>
    public bool Matches(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (Include is not null)
        {
            return Include.Contains(kind);
        }

        return Exclude is null || !Exclude.Contains(kind);
    }
}

/// <summary>
/// Well-known durable outbox record kinds.
/// </summary>
public static class OutboxKinds
{
    /// <summary>
    /// Internal restart-safe continuation signal consumed only by the continuation pump (DR-034).
    /// External message dispatchers never receive this kind.
    /// </summary>
    public const string Continue = "continue";
}

/// <summary>
/// Describes a recoverable durable timer claim request.
/// </summary>
public sealed record TimerClaimRequest(
    DateTimeOffset DueAtOrBefore,
    int MaxCount,
    DateTimeOffset ClaimedAt,
    TimeSpan LeaseDuration);

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
