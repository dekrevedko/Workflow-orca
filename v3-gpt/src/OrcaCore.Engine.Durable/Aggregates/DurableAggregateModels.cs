using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;
internal sealed record DurableDecision
{
    internal DurableDecision(
        IReadOnlyList<WorkflowEvent> events,
        CheckpointWrite? checkpoint = null,
        bool evictAfterCommit = false,
        IReadOnlyList<InboxWrite>? inboxOperations = null)
    {
        Events = events;
        Checkpoint = checkpoint;
        EvictAfterCommit = evictAfterCommit;
        InboxOperations = inboxOperations ?? [];
    }

    internal IReadOnlyList<WorkflowEvent> Events { get; }

    internal CheckpointWrite? Checkpoint { get; }

    internal bool EvictAfterCommit { get; }

    internal IReadOnlyList<InboxWrite> InboxOperations { get; }

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
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries,
    IReadOnlyList<DurableBufferedTimer> BufferedTimers,
    IReadOnlyList<DurableActiveChild> ActiveChildren,
    IReadOnlyList<DurableActiveChildGroup> ActiveChildGroups,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets,
    IReadOnlyList<DurableActiveExternalJob> ActiveExternalJobs,
    IReadOnlyList<DurableSagaForwardAction> CompletedSagaForwardActions,
    IReadOnlyList<DurableSagaCompensationAction> SagaCompensationActions,
    IReadOnlyList<DurableSagaRecoveryIntervention> SagaRecoveryInterventions,
    IReadOnlyList<string> RequestedSagaCompensationScopes)
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
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries,
    IReadOnlyList<DurableBufferedTimer> BufferedTimers,
    IReadOnlyList<DurableActiveChild> ActiveChildren,
    IReadOnlyList<DurableActiveChildGroup> ActiveChildGroups,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets,
    IReadOnlyList<DurableActiveExternalJob> ActiveExternalJobs,
    IReadOnlyList<DurableSagaForwardAction> CompletedSagaForwardActions,
    IReadOnlyList<DurableSagaCompensationAction> SagaCompensationActions,
    IReadOnlyList<DurableSagaRecoveryIntervention> SagaRecoveryInterventions,
    IReadOnlyList<string> RequestedSagaCompensationScopes,
    IReadOnlyList<EventId> RecordedParentResumeTokens,
    IReadOnlyList<EventId> ConsumedParentResumeTokens,
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
    DateTimeOffset RegisteredAt);

internal sealed record DurableActiveWait(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    WaitMode Mode = WaitMode.Resident,
    string? BranchId = null,
    TimerId? TimeoutTimerId = null);

internal sealed record DurableBufferedDelivery(
    EventId EventId,
    string EventName,
    CorrelationId CorrelationId,
    string? BranchId,
    string? PayloadContentType = null,
    byte[]? Payload = null);

internal sealed record DurablePendingResume(
    WaitId WaitId,
    EventId MatchedEventId,
    string? EventName,
    CorrelationId? CorrelationId,
    string? BranchId,
    string? PayloadContentType,
    byte[]? Payload,
    DateTimeOffset MatchedAt);

internal sealed record DurableBufferedTimer(
    TimerId TimerId,
    string WakeupName,
    DateTimeOffset BufferedAt);

internal sealed record DurableActiveChild(
    string GroupId,
    InstanceId ChildInstanceId,
    WaitId WaitId,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy,
    string? ItemSnapshot);

internal sealed record DurableActiveChildGroup(
    string GroupId,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy,
    int MaxConcurrency,
    int NextDispatchIndex,
    IReadOnlyList<WorkflowChildMaterialization> Children);

internal sealed record DurableCompletedChild(
    string GroupId,
    InstanceId ChildInstanceId,
    string? ItemSnapshot,
    DateTimeOffset CompletedAt);

internal sealed record DurableActiveExternalJob(
    string ExternalJobId,
    WaitId WaitId,
    TimerId? TimeoutTimerId);

internal sealed record DurableSagaForwardAction(
    string ScopeId,
    string ActionKey,
    string CompensationKey,
    DateTimeOffset CompletedAt);

internal sealed record DurableSagaCompensationAction(
    string ScopeId,
    string ActionKey,
    int Order,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ErrorSummary,
    SagaCompensationActionStatus Status);

internal sealed record DurableSagaRecoveryIntervention(
    string ScopeId,
    string ActionKey,
    string OperatorId,
    string RecoveryAction,
    string? Reason,
    DateTimeOffset RecordedAt,
    WorkflowStatus TargetStatus);

internal sealed record DurableStepCompletedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    DurableExecutionEnvelope Envelope)
{
    /// <summary>
    /// Gets the stream version the driver observed when deciding this command; the kernel
    /// rejects the commit as a conflict when the stream moved past it (DU-022).
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }

    /// <summary>
    /// Gets pending matched-wait resumes this advancement consumed.
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    /// <summary>
    /// Gets active waits released by this advancement (losing WhenFirst branches, timeout races).
    /// </summary>
    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    /// <summary>
    /// Gets active timers released by this advancement.
    /// </summary>
    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];
}

internal sealed record DurableStepFailedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string ErrorSummary,
    DurableExecutionEnvelope? Envelope = null)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

public sealed record DurableYieldCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    DurableExecutionEnvelope Envelope)
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

public sealed record DurableRunChildCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId ChildInstanceId,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    RunChildFailurePolicy FailurePolicy)
{
    /// <summary>
    /// Gets the execution-position envelope checkpointed atomically with the child dispatch
    /// (DR-011a); null for kernel-level callers outside driver advancement.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version the durable driver observed when it decided this command.
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

public sealed record DurableChildCompletedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId ChildInstanceId,
    WorkflowStatus ChildStatus,
    string? ErrorSummary);

public sealed record DurableRunChildrenCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    IReadOnlyList<string> ItemSnapshots,
    RunChildFailurePolicy FailurePolicy,
    int? MaxConcurrency = null,
    RunChildrenJoinPolicy JoinPolicy = RunChildrenJoinPolicy.WhenAll,
    RunChildrenResidualPolicy ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining)
{
    /// <summary>
    /// Gets the execution-position envelope checkpointed atomically with the group dispatch
    /// (DR-011a); null for kernel-level callers outside driver advancement.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version the durable driver observed when it decided this command.
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }
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
    /// <summary>
    /// Gets the execution-position envelope checkpointed atomically with the wait registration
    /// (DR-011a); null only for kernel-internal registrations outside driver advancement.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

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
}

internal sealed record DurableWaitMatchedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WaitId WaitId,
    EventId MatchedEventId);

internal sealed record DurablePauseCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt);

internal sealed record DurableResumeCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    ResumeBufferedDeliveries BufferedDeliveries = ResumeBufferedDeliveries.Replay);

internal enum ResumeBufferedDeliveries
{
    Replay,
    Discard
}

internal sealed record DurableCompleteCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string? OutcomeName,
    DurableExecutionEnvelope? Envelope = null)
{
    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public IReadOnlyList<WaitId> CancelWaitIds { get; init; } = [];

    public IReadOnlyList<TimerId> CancelTimerIds { get; init; } = [];
}

internal sealed record DurableFailCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string ErrorSummary,
    DurableExecutionEnvelope? Envelope = null)
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
