using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Declares how projection writes participate in a provider commit.
/// </summary>
public enum ProjectionCommitMode
{
    /// <summary>
    /// Projection updates are part of the same provider commit boundary as stream append.
    /// </summary>
    SameCommitBoundary
}

/// <summary>
/// Captures durable provider policy choices that affect port contracts.
/// </summary>
public static class ProviderCommitPolicy
{
    /// <summary>
    /// Gets the projection commit mode selected by IOQ-3.
    /// </summary>
    public static ProjectionCommitMode ProjectionMode => ProjectionCommitMode.SameCommitBoundary;
}

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
    public IReadOnlyList<WorkflowEvent> Events { get; init; } = [];

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
}

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
    public WorkflowStatus? Status { get; init; }

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
}

/// <summary>
/// Describes an inbox state write.
/// </summary>
public sealed record InboxWrite(EventId EventId, InboxRecordState State);

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
    public WorkflowInstanceSnapshot? InstanceSnapshot { get; init; }

    /// <summary>
    /// Gets the projected active wait for upsert operations.
    /// </summary>
    public ActiveWaitSnapshot? ActiveWait { get; init; }

    /// <summary>
    /// Gets the wait identity for remove operations.
    /// </summary>
    public WaitId? WaitId { get; init; }
}

/// <summary>
/// Describes a structured provider-side projection query.
/// </summary>
public sealed record WorkflowProjectionQuery
{
    /// <summary>
    /// Gets an unconstrained projection query.
    /// </summary>
    public static WorkflowProjectionQuery All { get; } = new();

    /// <summary>
    /// Gets an optional instance identity filter.
    /// </summary>
    public InstanceId? InstanceId { get; init; }

    /// <summary>
    /// Gets an optional parent instance identity filter.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets an optional root instance identity filter.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }

    /// <summary>
    /// Gets an optional definition identity filter.
    /// </summary>
    public DefinitionId? DefinitionId { get; init; }

    /// <summary>
    /// Gets an optional definition version filter.
    /// </summary>
    public DefinitionVersion? DefinitionVersion { get; init; }

    /// <summary>
    /// Gets an optional lifecycle status filter.
    /// </summary>
    public WorkflowStatus? Status { get; init; }

    /// <summary>
    /// Gets an optional active-wait event name filter.
    /// </summary>
    public string? ActiveWaitEventName { get; init; }

    /// <summary>
    /// Gets an optional active-wait correlation filter.
    /// </summary>
    public CorrelationId? ActiveWaitCorrelationId { get; init; }
}
