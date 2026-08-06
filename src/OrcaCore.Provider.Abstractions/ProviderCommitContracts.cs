using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using ProjectionActiveWaitSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Carries all writes for one accepted durable mutation.
/// </summary>
public sealed record ProviderCommitBatch
{
    /// <summary>
    /// Gets the workflow stream being appended.
    /// </summary>
    public required WorkflowStreamId StreamId { get; init; }

    /// <summary>
    /// Gets the expected stream version for optimistic concurrency.
    /// </summary>
    public required StreamVersion ExpectedVersion { get; init; }

    /// <summary>
    /// Gets the workflow events to append.
    /// </summary>
    public IReadOnlyList<DurableWorkflowEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets the optional checkpoint write.
    /// </summary>
    public CheckpointWrite? Checkpoint { get; init; }

    /// <summary>
    /// Gets inbox state updates included in the commit.
    /// </summary>
    public IReadOnlyList<InboxWrite> InboxOperations { get; init; } = [];

    /// <summary>
    /// Gets outbox records derived in the commit.
    /// </summary>
    public IReadOnlyList<OutboxWrite> OutboxRecords { get; init; } = [];

    /// <summary>
    /// Gets projection writes included in the commit boundary.
    /// </summary>
    public IReadOnlyList<ProjectionWrite> ProjectionOperations { get; init; } = [];

    /// <summary>
    /// Gets durable timer schedules included in the same commit boundary as the events that declared them.
    /// </summary>
    public IReadOnlyList<TimerScheduleRequest> TimerSchedules { get; init; } = [];

    /// <summary>
    /// Gets durable start idempotency keys bound in the same commit as the start event.
    /// </summary>
    public IReadOnlyList<StartIdempotencyWrite> StartIdempotencyOperations { get; init; } = [];
}

/// <summary>
/// Records the durable mapping from a caller-supplied start idempotency key to the instance that won it.
/// </summary>
public sealed record StartIdempotencyWrite(
    string IdempotencyKey,
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string DefinitionFingerprint,
    string InputFingerprint);

/// <summary>
/// Describes the result of a successful append.
/// </summary>
public sealed record AppendEventsResult(StreamVersion NewVersion);

/// <summary>
/// Describes one checkpoint payload write.
/// </summary>
public sealed record CheckpointWrite(
    InstanceId InstanceId,
    StreamVersion StreamVersion,
    string ContentType,
    byte[] Payload)
{
    /// <summary>
    /// Gets the workflow definition identity restored by this checkpoint when known.
    /// </summary>
    public DefinitionId? DefinitionId { get; init; }

    /// <summary>
    /// Gets the parent workflow instance restored by this checkpoint when known.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets the root workflow instance restored by this checkpoint when known.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }

    /// <summary>
    /// Gets the workflow definition version restored by this checkpoint when known.
    /// </summary>
    public DefinitionVersion? DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the lifecycle status restored by this checkpoint when known.
    /// </summary>
    public global::OrcaCore.WorkflowInstanceStatus? Status { get; init; }

    /// <summary>
    /// Gets the last completed step path restored by this checkpoint when known.
    /// </summary>
    public string? LastStepPath { get; init; }

    /// <summary>
    /// Gets the failure summary restored by this checkpoint when known.
    /// </summary>
    public string? ErrorSummary { get; init; }

    /// <summary>
    /// Gets the completion outcome restored by this checkpoint when known.
    /// </summary>
    public string? OutcomeName { get; init; }

    /// <summary>
    /// Gets the continue-as-new generation restored by this checkpoint.
    /// </summary>
    public int ContinueAsNewGeneration { get; init; }

    /// <summary>
    /// Gets engine runtime collections that must survive checkpoint compaction.
    /// </summary>
    public WorkflowRuntimeCheckpointState RuntimeState { get; init; } = WorkflowRuntimeCheckpointState.Empty;
}

/// <summary>
/// Captures non-business durable runtime state materialized into a checkpoint.
/// </summary>
public sealed record WorkflowRuntimeCheckpointState
{
    /// <summary>
    /// Gets an empty runtime-state checkpoint.
    /// </summary>
    public static WorkflowRuntimeCheckpointState Empty { get; } = new();

    /// <summary>
    /// Gets active durable timers.
    /// </summary>
    public IReadOnlyList<CheckpointActiveTimer> ActiveTimers { get; init; } = [];

    /// <summary>
    /// Gets active durable waits.
    /// </summary>
    public IReadOnlyList<CheckpointActiveWait> ActiveWaits { get; init; } = [];

    /// <summary>
    /// Gets active resource-pool tickets.
    /// </summary>
    public IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets { get; init; } = [];

    /// <summary>
    /// Gets matched-wait resume envelopes not yet consumed by a driver advancement commit.
    /// </summary>
    public IReadOnlyList<CheckpointPendingResume> PendingResumes { get; init; } = [];

    /// <summary>
    /// Gets the consecutive continuation failure count for the unresolved committed position.
    /// </summary>
    public int ContinuationFailureCount { get; init; }

    /// <summary>
    /// Gets the checkpoint position whose continuation attempts are failing.
    /// </summary>
    public StreamVersion? ContinuationFailurePositionStreamVersion { get; init; }

    /// <summary>
    /// Gets when another host may retry the failed continuation.
    /// </summary>
    public DateTimeOffset? ContinuationRetryNotBefore { get; init; }
}

/// <summary>
/// Checkpoint materialization of one matched-wait resume envelope awaiting driver consumption.
/// </summary>
public sealed record CheckpointPendingResume(
    WaitId WaitId,
    EventId MatchedEventId,
    string? EventName,
    CorrelationId? CorrelationId,
    string? BranchId,
    string? PayloadContentType,
    byte[]? Payload,
    DateTimeOffset MatchedAt)
{
    /// <summary>Gets the matched application-owned event-contract version, when event-backed.</summary>
    public int? EventContractVersion { get; init; }

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }
}

/// <summary>
/// Checkpoint materialization of one active timer.
/// </summary>
public sealed record CheckpointActiveTimer(
    TimerId TimerId,
    DateTimeOffset FireAt,
    string WakeupName,
    DateTimeOffset RegisteredAt)
{
    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }
}

/// <summary>
/// Checkpoint materialization of one active wait.
/// </summary>
public sealed record CheckpointActiveWait(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    WaitMode Mode,
    string? BranchId,
    TimerId? TimeoutTimerId = null)
{
    /// <summary>Gets the positive application-owned event-contract version.</summary>
    public int EventContractVersion { get; init; } = 1;

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }
}

/// <summary>
/// Describes an inbox state write.
/// </summary>
public sealed record InboxWrite(EventId EventId, InboxRecordState State)
{
    /// <summary>
    /// Gets the deterministic normalized envelope fingerprint for an initial inbox write.
    /// State-only transitions preserve the fingerprint already stored for the target/event pair.
    /// </summary>
    public string? EnvelopeFingerprint { get; init; }
}

/// <summary>
/// Describes a durable inbox record state.
/// </summary>
public enum InboxRecordState
{
    /// <summary>
    /// The delivery was received but not yet applied.
    /// </summary>
    Received,

    /// <summary>
    /// The delivery was applied to workflow state.
    /// </summary>
    Applied,

    /// <summary>
    /// The delivery was a duplicate and was ignored.
    /// </summary>
    DuplicateIgnored,

    /// <summary>
    /// The delivery cannot be processed automatically.
    /// </summary>
    Poisoned,

    /// <summary>
    /// The delivery was discarded during resume from pause.
    /// </summary>
    DiscardedOnResume
}

/// <summary>
/// Describes an outbox write derived from committed events.
/// </summary>
public sealed record OutboxWrite(OutboxRecordId OutboxRecordId, string Kind, byte[] Payload);

/// <summary>
/// Describes durable outbox dispatch state.
/// </summary>
public enum OutboxRecordState
{
    /// <summary>
    /// The record is ready to be claimed.
    /// </summary>
    Pending,

    /// <summary>
    /// The record was dispatched successfully.
    /// </summary>
    Dispatched,

    /// <summary>
    /// The record is claimed by a dispatcher.
    /// </summary>
    Claimed,

    /// <summary>
    /// The dispatch failed and may be retried.
    /// </summary>
    Retryable,

    /// <summary>
    /// The dispatch failed permanently.
    /// </summary>
    Poisoned
}

/// <summary>
/// Describes a projection write kind.
/// </summary>
public enum ProjectionOperationKind
{
    /// <summary>
    /// Upserts an instance summary projection.
    /// </summary>
    UpsertSummary,

    /// <summary>
    /// Upserts an active wait projection.
    /// </summary>
    UpsertActiveWait,

    /// <summary>
    /// Removes an active wait projection.
    /// </summary>
    RemoveActiveWait,

    /// <summary>
    /// Appends a history projection entry.
    /// </summary>
    AppendHistory
}

/// <summary>
/// Describes one projection update in a provider commit.
/// </summary>
public sealed record ProjectionWrite(InstanceId InstanceId, ProjectionOperationKind Kind)
{
    /// <summary>
    /// Gets the projected instance summary for upsert operations.
    /// </summary>
    public ProjectionWorkflowInstanceSnapshot? InstanceSnapshot { get; init; }

    /// <summary>
    /// Gets the projected active wait for upsert operations.
    /// </summary>
    public ProjectionActiveWaitSnapshot? ActiveWait { get; init; }

    /// <summary>
    /// Gets the wait identity for remove operations.
    /// </summary>
    public WaitId? WaitId { get; init; }

    /// <summary>
    /// Gets a history entry for append-history projection operations.
    /// </summary>
    public ProjectionHistoryWrite? History { get; init; }
}

/// <summary>
/// Describes one durable history projection entry.
/// </summary>
public sealed record ProjectionHistoryWrite(
    Guid HistoryId,
    DateTimeOffset RecordedAt,
    string Kind,
    string PayloadJson);
