using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

namespace OrcaCore.Engine.Durable.Aggregates;
internal sealed record DurableDecision
{
    internal DurableDecision(
        IReadOnlyList<DurableWorkflowEvent> events,
        CheckpointWrite? checkpoint = null,
        bool evictAfterCommit = false,
        IReadOnlyList<InboxWrite>? inboxOperations = null,
        IReadOnlyList<InboxRouteMutation>? inboxRouteMutations = null,
        IReadOnlyList<OutboxWrite>? outboxRecords = null)
    {
        Events = events;
        Checkpoint = checkpoint;
        EvictAfterCommit = evictAfterCommit;
        InboxOperations = inboxOperations ?? [];
        InboxRouteMutations = inboxRouteMutations ?? [];
        OutboxRecords = outboxRecords ?? [];
    }

    internal IReadOnlyList<DurableWorkflowEvent> Events { get; }

    internal CheckpointWrite? Checkpoint { get; }

    internal bool EvictAfterCommit { get; }

    internal IReadOnlyList<InboxWrite> InboxOperations { get; }

    internal IReadOnlyList<InboxRouteMutation> InboxRouteMutations { get; }

    internal IReadOnlyList<OutboxWrite> OutboxRecords { get; }

    internal static DurableDecision Empty { get; } = new([]);
}

internal sealed record DurableAggregateSnapshot(
    InstanceId InstanceId,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    InstanceId? ParentInstanceId,
    InstanceId? RootInstanceId,
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    int ContinueAsNewGeneration,
    IReadOnlyList<DurableActiveTimer> ActiveTimers,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets)
{
    /// <summary>
    /// Gets matched-wait resume envelopes not yet consumed by driver advancement.
    /// </summary>
    internal IReadOnlyList<DurablePendingResume> PendingResumes { get; init; } = [];

    /// <summary>
    /// Gets the serialized start input content type, when the driver started the instance.
    /// </summary>
    internal string? StartInputContentType { get; init; }

    /// <summary>
    /// Gets the serialized start input, when the driver started the instance.
    /// </summary>
    internal byte[]? StartInputPayload { get; init; }

    /// <summary>
    /// Gets the park reason when the instance status is Parked.
    /// </summary>
    internal DurableParkReason? ParkReason { get; init; }
}

internal sealed record DurableAggregateCheckpoint(
    InstanceId InstanceId,
    StreamVersion StreamVersion,
    InstanceId? ParentInstanceId,
    InstanceId? RootInstanceId,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    int ContinueAsNewGeneration,
    IReadOnlyList<DurableActiveTimer> ActiveTimers,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets,
    string ContentType,
    byte[] Payload)
{
    /// <summary>
    /// Gets matched-wait resume envelopes not yet consumed by driver advancement.
    /// </summary>
    internal IReadOnlyList<DurablePendingResume> PendingResumes { get; init; } = [];

    internal int ContinuationFailureCount { get; init; }

    internal StreamVersion? ContinuationFailurePositionStreamVersion { get; init; }

    internal DateTimeOffset? ContinuationRetryNotBefore { get; init; }
}

internal sealed record DurableActiveTimer(
    TimerId TimerId,
    DateTimeOffset FireAt,
    string WakeupName,
    DateTimeOffset RegisteredAt)
{
    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }
}

internal sealed record DurableActiveWait(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    WaitMode Mode = WaitMode.Resident,
    string? BranchId = null,
    TimerId? TimeoutTimerId = null)
{
    public int EventContractVersion { get; init; } = 1;

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }
}

internal sealed record DurablePendingResume(
    WaitId WaitId,
    EventId MatchedEventId,
    string? EventName,
    CorrelationId? CorrelationId,
    string? BranchId,
    string? PayloadContentType,
    byte[]? Payload,
    DateTimeOffset MatchedAt)
{
    public int? EventContractVersion { get; init; }

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }
}

internal sealed record DurableStepCompletedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    DurableCheckpointPayload Envelope)
{
    public StepOperationId? StepOperationId { get; init; }

    public int? StepAttemptNumber { get; init; }

    /// <summary>
    /// Gets the stream version the driver observed when deciding this command; the kernel
    /// rejects the commit as a conflict when the stream moved past it (DU-022).
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }

    /// <summary>Gets external outbox records committed atomically with this advancement.</summary>
    public IReadOnlyList<OutboxWrite> OutboxRecords { get; init; } = [];

    /// <summary>
    /// Gets pending matched-wait resumes this advancement consumed.
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    /// <summary>
    /// Gets active waits released by this advancement (cancelled branches and timeout races).
    /// </summary>
    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    /// <summary>
    /// Gets active timers released by this advancement.
    /// </summary>
    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];

    /// <summary>
    /// Gets fibers that became terminal in this advancement. The aggregate releases every
    /// durable artifact still owned by these fibers in the same commit.
    /// </summary>
    public IReadOnlyList<FiberId> TerminalFiberIds { get; init; } = [];

    /// <summary>
    /// Gets scoped resource holders whose exact tickets must be released in this commit.
    /// </summary>
    public IReadOnlyList<string> ReleaseResourceHolderKeys { get; init; } = [];

}

internal sealed record DurableStepFailedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string ErrorSummary,
    DurableCheckpointPayload? Envelope = null)
{
    public StepOperationId? StepOperationId { get; init; }

    public int? StepAttemptNumber { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];

    public IReadOnlyList<FiberId> TerminalFiberIds { get; init; } = [];

    public bool PreserveOwnership { get; init; }
}

internal sealed record DurableFiberFailedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string ErrorSummary,
    DurableCheckpointPayload Envelope)
{
    public StepOperationId? StepOperationId { get; init; }

    public int? StepAttemptNumber { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];

    public IReadOnlyList<FiberId> TerminalFiberIds { get; init; } = [];

    public bool PreserveOwnership { get; init; }
}

internal sealed record DurableYieldCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    DurableCheckpointPayload Envelope)
{
    /// <summary>
    /// Gets the stream version the driver observed when deciding this command.
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }

    /// <summary>
    /// Gets pending matched-wait resumes this yield's chunk consumed.
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];
}

internal sealed record DurableWaitRegisteredCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    WaitMode Mode = WaitMode.Resident,
    string? BranchId = null)
{
    public int EventContractVersion { get; init; } = 1;

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }

    /// <summary>
    /// Gets the execution-position envelope checkpointed atomically with the wait registration
    /// (DR-011a); null only for kernel-internal registrations outside driver advancement.
    /// </summary>
    public DurableCheckpointPayload? Envelope { get; init; }

    /// <summary>
    /// Gets the timeout timer registered atomically with the wait for timeout races, when set.
    /// </summary>
    public TimerId? TimeoutTimerId { get; init; }

    /// <summary>
    /// Gets when the timeout timer fires, when <see cref="TimeoutTimerId"/> is set.
    /// </summary>
    public DateTimeOffset? TimeoutFireAt { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];

    /// <summary>Gets the provider snapshot serialized with this wait registration.</summary>
    public InboxMatchSnapshot? InboxMatch { get; init; }
}

internal sealed record DurableWaitMatchedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WaitId WaitId,
    EventId MatchedEventId);

internal sealed record DurableCompleteCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string? OutcomeName,
    DurableCheckpointPayload? Envelope = null)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];

    public IReadOnlyList<FiberId> TerminalFiberIds { get; init; } = [];

    public bool PreserveOwnership { get; init; }
}

internal sealed record DurableFailCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string ErrorSummary,
    DurableCheckpointPayload? Envelope = null)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }

    public bool PreserveOwnership { get; init; }
}

internal sealed record DurableTimeoutCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string ErrorSummary,
    DurableCheckpointPayload Envelope)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }

    public bool PreserveOwnership { get; init; }
}

internal sealed record DurableTerminalLifecycleCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WorkflowStatus Status,
    DurableCheckpointPayload? Envelope)
{
    public IReadOnlySet<string> PreserveResourceHolderKeys { get; init; } =
        new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyList<DurableQueuedResourceCancellation> QueuedResourceCancellations { get; init; } = [];
}

internal sealed record DurableQueuedResourceCancellation(
    string HolderKey,
    FiberId? FiberId,
    ScopeId? ScopeId);

internal sealed record DurableLeaseStopConfirmedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string HolderKey,
    DurableCheckpointPayload Envelope)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

internal sealed record DurableParkCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    DurableParkReason Reason,
    string ErrorSummary,
    int FailedAttemptCount,
    StreamVersion? PositionStreamVersion)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

internal sealed record DurableUnparkCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    StreamVersion ExpectedStreamVersion);

internal sealed record DurableContinuationAttemptFailedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string ErrorSummary,
    StreamVersion? PositionStreamVersion,
    DateTimeOffset NextEligibleAt,
    StreamVersion ExpectedStreamVersion);

internal sealed record DurableContinuationAttemptResetCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    StreamVersion ExpectedStreamVersion);
