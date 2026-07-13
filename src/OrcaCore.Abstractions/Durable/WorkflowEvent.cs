using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Represents a durable engine-owned workflow fact appended to an instance stream.
/// </summary>
public abstract record WorkflowEvent
{
    /// <summary>
    /// Gets the durable event identity.
    /// </summary>
    public required EventId EventId { get; init; }

    /// <summary>
    /// Gets the workflow instance identity.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the command that caused this event.
    /// </summary>
    public required CommandId CommandId { get; init; }

    /// <summary>
    /// Gets the causal chain identity for this event.
    /// </summary>
    public required CausationId CausationId { get; init; }

    /// <summary>
    /// Gets when the fact occurred.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets the parent workflow instance when this fact belongs to child work.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets the root workflow instance for the workflow tree.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }
}

/// <summary>
/// Records that a workflow instance started and bound to a definition version.
/// </summary>
public sealed record WorkflowStartedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the workflow definition identity.
    /// </summary>
    public required DefinitionId DefinitionId { get; init; }

    /// <summary>
    /// Gets the workflow definition version bound at start.
    /// </summary>
    public required DefinitionVersion DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the durable start-or-get idempotency key when the instance was started that way.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the content type of the serialized start input, when the driver started the instance.
    /// </summary>
    public string? InputContentType { get; init; }

    /// <summary>
    /// Gets the serialized start input, durable until the first driver checkpoint commits.
    /// </summary>
    public byte[]? InputPayload { get; init; }
}

/// <summary>
/// Records that a workflow instance rolled over to a new baseline generation.
/// </summary>
public sealed record WorkflowContinuedAsNewEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the previous stream version that was sealed by the rollover.
    /// </summary>
    public required StreamVersion PreviousStreamVersion { get; init; }

    /// <summary>
    /// Gets the monotonic generation number for the new baseline.
    /// </summary>
    public required int Generation { get; init; }
}

/// <summary>
/// Records that a workflow step completed.
/// </summary>
public sealed record WorkflowStepCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the durable node path for the completed step.
    /// </summary>
    public required string StepPath { get; init; }
}

/// <summary>
/// Records that a workflow step failed.
/// </summary>
public sealed record WorkflowStepFailedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the durable node path for the failed step.
    /// </summary>
    public required string StepPath { get; init; }

    /// <summary>
    /// Gets the failure summary.
    /// </summary>
    public required string ErrorSummary { get; init; }
}

/// <summary>
/// Records that a workflow wait was registered.
/// </summary>
public sealed record WorkflowWaitRegisteredEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the wait identity.
    /// </summary>
    public required WaitId WaitId { get; init; }

    /// <summary>
    /// Gets the wait event name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the wait correlation identity.
    /// </summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>
    /// Gets whether the wait is resident or cold.
    /// </summary>
    public WaitMode Mode { get; init; } = WaitMode.Resident;

    /// <summary>
    /// Gets the parallel branch identity when this wait is branch-scoped.
    /// </summary>
    public string? BranchId { get; init; }

    /// <summary>
    /// Gets the durable timeout timer racing this wait, when the wait was registered with a
    /// timeout. The kernel cancels the loser when either side wins (DR-010 timeout races).
    /// </summary>
    public TimerId? TimeoutTimerId { get; init; }
}

/// <summary>
/// Records that a workflow wait matched an inbound event.
/// </summary>
public sealed record WorkflowWaitMatchedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the matched wait identity.
    /// </summary>
    public required WaitId WaitId { get; init; }

    /// <summary>
    /// Gets the inbound event identity that matched the wait.
    /// </summary>
    public required EventId MatchedEventId { get; init; }

    /// <summary>
    /// Gets the matched event name, recorded so the resume envelope survives checkpoint
    /// compaction between the match and its consumption by the driver.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// Gets the matched event correlation identity.
    /// </summary>
    public CorrelationId? CorrelationId { get; init; }

    /// <summary>
    /// Gets the matched event branch scope when branch-targeted.
    /// </summary>
    public string? BranchId { get; init; }

    /// <summary>
    /// Gets the content type of the serialized matched payload, when one was delivered.
    /// </summary>
    public string? PayloadContentType { get; init; }

    /// <summary>
    /// Gets the serialized matched payload, when one was delivered.
    /// </summary>
    public byte[]? Payload { get; init; }
}

/// <summary>
/// Records that an active wait was cancelled without matching (timeout race loss, losing
/// WhenFirst branch, or explicit driver release).
/// </summary>
public sealed record WorkflowWaitCancelledEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the cancelled wait identity.
    /// </summary>
    public required WaitId WaitId { get; init; }
}

/// <summary>
/// Records that an active timer was cancelled without firing (timeout race loss, losing
/// WhenFirst branch, or explicit driver release).
/// </summary>
public sealed record WorkflowTimerCancelledEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the cancelled timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }
}

/// <summary>
/// Records that the durable driver consumed a pending matched-wait resume envelope; the
/// consuming advancement committed in the same boundary.
/// </summary>
public sealed record WorkflowResumeConsumedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the wait whose pending resume was consumed.
    /// </summary>
    public required WaitId WaitId { get; init; }
}

/// <summary>
/// Records that a durable instance was parked on an unresolved fault (DR-017).
/// </summary>
public sealed record WorkflowParkedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the park reason category.
    /// </summary>
    public required DurableParkReason Reason { get; init; }

    /// <summary>
    /// Gets the operator-readable error summary.
    /// </summary>
    public required string ErrorSummary { get; init; }

    /// <summary>
    /// Gets how many failed advancement attempts preceded parking.
    /// </summary>
    public required int FailedAttemptCount { get; init; }

    /// <summary>
    /// Gets the checkpoint stream version whose persisted position the instance parks on;
    /// null when no driver checkpoint exists yet.
    /// </summary>
    public StreamVersion? PositionStreamVersion { get; init; }
}

/// <summary>
/// Records that a parked durable instance was explicitly re-armed and resumes from its
/// persisted position (DR-017).
/// </summary>
public sealed record WorkflowUnparkedEvent : WorkflowEvent;

/// <summary>
/// Records one failed continuation advancement and its durable retry deadline (DR-036).
/// </summary>
public sealed record WorkflowContinuationAttemptFailedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the consecutive failure count for the unresolved committed position.
    /// </summary>
    public required int AttemptCount { get; init; }

    /// <summary>
    /// Gets the failed checkpoint position, when one exists.
    /// </summary>
    public StreamVersion? PositionStreamVersion { get; init; }

    /// <summary>
    /// Gets when another host may retry this continuation.
    /// </summary>
    public required DateTimeOffset NextEligibleAt { get; init; }

    /// <summary>
    /// Gets the latest operator-readable failure summary.
    /// </summary>
    public required string ErrorSummary { get; init; }
}

/// <summary>
/// Records that continuation advancement recovered and clears its consecutive failure state.
/// </summary>
public sealed record WorkflowContinuationAttemptResetEvent : WorkflowEvent;

/// <summary>
/// Records that a durable timer was scheduled for one workflow instance.
/// </summary>
public sealed record WorkflowTimerScheduledEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the durable timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }

    /// <summary>
    /// Gets when the timer becomes eligible to fire.
    /// </summary>
    public required DateTimeOffset FireAt { get; init; }

    /// <summary>
    /// Gets the logical wake-up name.
    /// </summary>
    public required string WakeupName { get; init; }
}

/// <summary>
/// Records that a durable timer fired and was consumed.
/// </summary>
public sealed record WorkflowTimerFiredEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the durable timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }
}

/// <summary>
/// Records that a parent scheduled a child workflow and entered a synthetic wait.
/// </summary>
public sealed record WorkflowChildScheduledEvent : WorkflowEvent
{
    public required InstanceId ChildInstanceId { get; init; }

    public required DefinitionId ChildDefinitionId { get; init; }

    public required DefinitionVersion ChildDefinitionVersion { get; init; }

    public required WaitId WaitId { get; init; }

    public required RunChildFailurePolicy FailurePolicy { get; init; }
}

/// <summary>
/// Records that a parent materialized a deterministic child workflow group.
/// </summary>
public sealed record WorkflowChildrenScheduledEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required DefinitionId ChildDefinitionId { get; init; }

    public required DefinitionVersion ChildDefinitionVersion { get; init; }

    public required RunChildFailurePolicy FailurePolicy { get; init; }

    public required RunChildrenJoinPolicy JoinPolicy { get; init; }

    public required RunChildrenResidualPolicy ResidualPolicy { get; init; }

    public required int TotalItemCount { get; init; }

    public required int InitialDispatchCount { get; init; }

    public required int NextDispatchIndex { get; init; }

    public required int MaxConcurrency { get; init; }

    public required IReadOnlyList<WorkflowChildMaterialization> Children { get; init; }
}

public sealed record WorkflowChildMaterialization
{
    public required int Index { get; init; }

    public required InstanceId ChildInstanceId { get; init; }

    public required DefinitionId ChildDefinitionId { get; init; }

    public required DefinitionVersion ChildDefinitionVersion { get; init; }

    public required string ItemSnapshot { get; init; }
}

/// <summary>
/// Records that a throttled child group advanced and dispatched more children.
/// </summary>
public sealed record WorkflowChildrenDispatchedEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required int PreviousDispatchIndex { get; init; }

    public required int NextDispatchIndex { get; init; }

    public required IReadOnlyList<WorkflowChildMaterialization> Children { get; init; }
}

/// <summary>
/// Records that a child workflow reached a terminal status observed by its parent.
/// </summary>
public sealed record WorkflowChildCompletedEvent : WorkflowEvent
{
    public required InstanceId ChildInstanceId { get; init; }

    public required WorkflowStatus ChildStatus { get; init; }

    public string? ErrorSummary { get; init; }
}

/// <summary>
/// Records the durable token that authorizes exactly one parent resume for a child group.
/// </summary>
public sealed record WorkflowParentResumeTokenRecordedEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required EventId ResumeTokenId { get; init; }
}

/// <summary>
/// Records that a parent resume token was consumed by the parent continuation driver.
/// </summary>
public sealed record WorkflowParentResumeTokenConsumedEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required EventId ResumeTokenId { get; init; }
}

/// <summary>
/// Records durable residual child handling intent before parent resume.
/// </summary>
public sealed record WorkflowChildResidualIntentRecordedEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required RunChildrenResidualPolicy ResidualPolicy { get; init; }

    public required IReadOnlyList<InstanceId> ResidualChildInstanceIds { get; init; }
}

/// <summary>
/// Records explicit compensation workflows materialized for completed children in a group.
/// </summary>
public sealed record WorkflowChildCompensationScheduledEvent : WorkflowEvent
{
    public required string GroupId { get; init; }

    public required DefinitionId CompensationDefinitionId { get; init; }

    public required DefinitionVersion CompensationDefinitionVersion { get; init; }

    public required IReadOnlyList<WorkflowChildCompensationMaterialization> Compensations { get; init; }
}

public sealed record WorkflowChildCompensationMaterialization
{
    public required int Index { get; init; }

    public required InstanceId SourceChildInstanceId { get; init; }

    public required InstanceId CompensationInstanceId { get; init; }

    public required string ItemSnapshot { get; init; }
}

/// <summary>
/// Records that resource-pool tickets were acquired for a guarded holder.
/// </summary>
public sealed record WorkflowResourcePoolAcquiredEvent : WorkflowEvent
{
    public required string HolderKey { get; init; }

    public required IReadOnlyList<ResourcePoolTicket> Tickets { get; init; }
}

/// <summary>
/// Records that resource-pool acquisition suspended the instance as a cold wait.
/// </summary>
public sealed record WorkflowResourcePoolQueuedEvent : WorkflowEvent
{
    public required WaitId WaitId { get; init; }

    public required string HolderKey { get; init; }

    public required IReadOnlyList<ResourcePoolRequirement> Requirements { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Records that resource-pool tickets were released for a guarded holder.
/// </summary>
public sealed record WorkflowResourcePoolReleasedEvent : WorkflowEvent
{
    public required string HolderKey { get; init; }

    public required IReadOnlyList<ResourcePoolTicket> Tickets { get; init; }
}

/// <summary>
/// Records that an external job start command is committed for dispatch.
/// </summary>
public sealed record WorkflowExternalJobStartedEvent : WorkflowEvent
{
    public required string ExternalJobId { get; init; }

    public required byte[] Payload { get; init; }

    public required WaitId WaitId { get; init; }

    public TimerId? TimeoutTimerId { get; init; }

    public DateTimeOffset? TimeoutAt { get; init; }
}

/// <summary>
/// Records that an external job completion report resumed the waiting workflow.
/// </summary>
public sealed record WorkflowExternalJobCompletedEvent : WorkflowEvent
{
    public required string ExternalJobId { get; init; }

    public required EventId CompletionEventId { get; init; }
}

/// <summary>
/// Records that external job timeout handling won the race.
/// </summary>
public sealed record WorkflowExternalJobTimedOutEvent : WorkflowEvent
{
    public required string ExternalJobId { get; init; }
}

/// <summary>
/// Records durable intent to stop external work.
/// </summary>
public sealed record WorkflowExternalJobStopRequestedEvent : WorkflowEvent
{
    public required string ExternalJobId { get; init; }
}

/// <summary>
/// Records that a timer firing was accepted while advancement was paused.
/// </summary>
public sealed record WorkflowTimerBufferedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the durable timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }

    /// <summary>
    /// Gets the logical wake-up name.
    /// </summary>
    public required string WakeupName { get; init; }
}

/// <summary>
/// Records that a workflow instance was paused at a safe boundary.
/// </summary>
public sealed record WorkflowPausedEvent : WorkflowEvent;

/// <summary>
/// Records that a paused workflow instance resumed.
/// </summary>
public sealed record WorkflowResumedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets how buffered deliveries were handled.
    /// </summary>
    public required string BufferHandling { get; init; }
}

/// <summary>
/// Records that an inbound delivery was buffered until a matching wait is available or the instance resumes.
/// </summary>
public sealed record WorkflowDeliveryBufferedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the buffered inbound event identity.
    /// </summary>
    public required EventId BufferedEventId { get; init; }

    /// <summary>
    /// Gets the buffered inbound event name.
    /// </summary>
    public required string EventName { get; init; }

    /// <summary>
    /// Gets the buffered inbound event correlation.
    /// </summary>
    public required CorrelationId CorrelationId { get; init; }

    /// <summary>
    /// Gets the parallel branch identity when the inbound event is branch-scoped.
    /// </summary>
    public string? BranchId { get; init; }

    /// <summary>
    /// Gets the content type of the serialized buffered payload, when one was delivered.
    /// </summary>
    public string? PayloadContentType { get; init; }

    /// <summary>
    /// Gets the serialized buffered payload, preserved so a later match resumes with it.
    /// </summary>
    public byte[]? Payload { get; init; }
}

/// <summary>
/// Records that a paused delivery was discarded on resume.
/// </summary>
public sealed record WorkflowDeliveryDiscardedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the discarded inbound event identity.
    /// </summary>
    public required EventId DiscardedEventId { get; init; }
}

/// <summary>
/// Records that a workflow reached successful completion.
/// </summary>
public sealed record WorkflowCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the optional named end outcome.
    /// </summary>
    public string? OutcomeName { get; init; }
}

/// <summary>
/// Records that a workflow entered a terminal lifecycle status.
/// </summary>
public sealed record WorkflowTerminalEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the terminal workflow status.
    /// </summary>
    public required WorkflowStatus Status { get; init; }
}

/// <summary>
/// Records that a compensatable saga forward action completed successfully.
/// </summary>
public sealed record SagaForwardActionCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable forward action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the stable compensation action key to run if this forward action is compensated.
    /// </summary>
    public required string CompensationKey { get; init; }
}

/// <summary>
/// Records that a saga forward action timed out and which compensation policy applied.
/// </summary>
public sealed record SagaForwardActionTimedOutEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable forward action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets whether timeout policy required compensating the scope.
    /// </summary>
    public required bool CompensateScope { get; init; }
}

/// <summary>
/// Records that compensation was requested for a saga scope.
/// </summary>
public sealed record SagaCompensationRequestedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets an optional operator or policy reason for the compensation request.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Records that one saga compensating action started.
/// </summary>
public sealed record SagaCompensationStartedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable compensating action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the deterministic zero-based compensation order.
    /// </summary>
    public required int Order { get; init; }
}

/// <summary>
/// Records that one saga compensating action completed.
/// </summary>
public sealed record SagaCompensationCompletedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable compensating action key.
    /// </summary>
    public required string ActionKey { get; init; }
}

/// <summary>
/// Records that one saga compensating action failed.
/// </summary>
public sealed record SagaCompensationFailedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable compensating action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the compensation failure summary.
    /// </summary>
    public required string ErrorSummary { get; init; }
}

/// <summary>
/// Records an operator recovery intervention for a compensation-failed saga.
/// </summary>
public sealed record SagaManualRecoveryRecordedEvent : WorkflowEvent
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable compensating action key affected by the intervention.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the operator identity supplied by the caller.
    /// </summary>
    public required string OperatorId { get; init; }

    /// <summary>
    /// Gets the recovery action name allowed by policy.
    /// </summary>
    public required string RecoveryAction { get; init; }

    /// <summary>
    /// Gets an optional operator-supplied reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the terminal status produced by the recovery action.
    /// </summary>
    public required WorkflowStatus TargetStatus { get; init; }
}
