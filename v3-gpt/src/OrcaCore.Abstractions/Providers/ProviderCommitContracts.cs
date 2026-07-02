using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;

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
    byte[] Payload);

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
public sealed record ProjectionWrite(InstanceId InstanceId, ProjectionOperationKind Kind);
