using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Represents a durable workflow command requested against one instance.
/// </summary>
public abstract record WorkflowCommand
{
    /// <summary>
    /// Gets the command identity.
    /// </summary>
    public required CommandId CommandId { get; init; }

    /// <summary>
    /// Gets the workflow instance identity.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets when the command was requested.
    /// </summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// Gets the parent workflow instance when this command starts or advances child work.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets the root workflow instance for the current workflow tree.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }
}

/// <summary>
/// Requests that a durable workflow instance starts under a specific definition version.
/// </summary>
public sealed record StartWorkflowCommand : WorkflowCommand
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
    /// Gets an optional caller-supplied key used to make start-or-get durable across restarts.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Gets the content type of the serialized start input, when the driver starts the instance.
    /// The input is committed with the start fact so a replacement host can run Init after a crash
    /// that precedes the first checkpoint (DR-031/DR-034).
    /// </summary>
    public string? InputContentType { get; init; }

    /// <summary>
    /// Gets the serialized start input committed with the start fact.
    /// </summary>
    public byte[]? InputPayload { get; init; }
}

/// <summary>
/// Requests that a durable workflow resumes from a matching inbound event.
/// </summary>
public sealed record DeliverEventCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the normalized inbound event envelope.
    /// </summary>
    public required EventEnvelope Envelope { get; init; }
}

/// <summary>
/// Requests that a durable timer wake-up be recorded and scheduled.
/// </summary>
public sealed record ScheduleTimerCommand : WorkflowCommand
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

    /// <summary>
    /// Gets the execution-position envelope committed with the timer schedule when the durable
    /// driver suspends an instance on a timer/delay node (DR-011a). Null for timers scheduled
    /// outside driver advancement.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version the durable driver observed when it decided this command; the
    /// kernel rejects the command as a conflict when the stream moved past it (DU-022).
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

/// <summary>
/// Requests that a due durable timer wake-up be applied to its workflow instance.
/// </summary>
public sealed record FireTimerCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the durable timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }
}

/// <summary>
/// Requests cooperative cancellation for a durable workflow instance.
/// </summary>
public sealed record CancelWorkflowCommand : WorkflowCommand;

/// <summary>
/// Requests idempotent consumption of a parent resume token recorded for a child group.
/// </summary>
public sealed record ConsumeParentResumeTokenCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the child group whose parent continuation is being consumed.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Gets the resume token identity to consume.
    /// </summary>
    public required EventId ResumeTokenId { get; init; }

    /// <summary>
    /// Gets the driver position checkpointed atomically with token consumption.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets matched child-completion resumes consumed by this parent advancement.
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    /// <summary>
    /// Gets the stream version observed when the driver decided this command.
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

/// <summary>
/// Requests forced termination for a durable workflow instance.
/// </summary>
public sealed record TerminateWorkflowCommand : WorkflowCommand;

/// <summary>
/// Requests durable history rollover while preserving the logical workflow instance identity.
/// </summary>
public sealed record ContinueAsNewCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the content type for the new baseline state payload.
    /// </summary>
    public required string StateContentType { get; init; }

    /// <summary>
    /// Gets the serialized state payload for the new baseline generation.
    /// </summary>
    public required byte[] StatePayload { get; init; }

    /// <summary>
    /// Gets the fresh-generation state and execution position committed by the durable driver.
    /// Null preserves the kernel-level raw-baseline behavior for existing callers.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version observed by the durable driver.
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }
}

/// <summary>
/// Explicitly requests durable compensation for completed children in one child group.
/// </summary>
public sealed record CompensateChildGroupCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the child group identity to compensate.
    /// </summary>
    public required string GroupId { get; init; }

    /// <summary>
    /// Gets the compensation workflow definition identity.
    /// </summary>
    public required DefinitionId CompensationDefinitionId { get; init; }

    /// <summary>
    /// Gets the compensation workflow definition version.
    /// </summary>
    public required DefinitionVersion CompensationDefinitionVersion { get; init; }
}

/// <summary>
/// Requests durable acquisition of all resource-pool requirements for one guarded holder.
/// </summary>
public sealed record AcquireResourcePoolCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the guarded holder key within the workflow instance.
    /// </summary>
    public required string HolderKey { get; init; }

    /// <summary>
    /// Gets the pool requirements to acquire atomically.
    /// </summary>
    public required IReadOnlyList<ResourcePoolRequirement> Requirements { get; init; }

    /// <summary>
    /// Gets when granted tickets should expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets the driver-chosen wait identity used when the acquisition queues, so the persisted
    /// execution position can reference the queued wait it suspends on (DR-011a). Null lets
    /// the kernel choose.
    /// </summary>
    public WaitId? WaitId { get; init; }

    /// <summary>
    /// Gets the execution-position envelope committed with the acquisition (DR-011a); null for
    /// kernel-level callers.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version this command was decided against; the commit is rejected when
    /// the stream moved (DU-022).
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }

    /// <summary>
    /// Gets pending resumes whose matched events fed the step that produced this command;
    /// their consumption commits atomically with it (DR-011a).
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];
}

/// <summary>
/// Requests dispatch of external work followed by a durable correlated wait.
/// </summary>
public sealed record RunExternalJobCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the provider-neutral external job identity.
    /// </summary>
    public required string ExternalJobId { get; init; }

    /// <summary>
    /// Gets the provider-neutral start payload.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// Gets durable-pool requirements that must be granted before dispatch.
    /// </summary>
    public IReadOnlyList<ResourcePoolRequirement> Requirements { get; init; } = [];

    /// <summary>
    /// Gets when the external job times out.
    /// </summary>
    public DateTimeOffset? TimeoutAt { get; init; }

    /// <summary>
    /// Gets the driver-chosen wait identity for the job's completion wait (or the queued
    /// acquisition wait), so the persisted execution position can reference the wait it
    /// suspends on (DR-011a). Null lets the kernel choose.
    /// </summary>
    public WaitId? WaitId { get; init; }

    /// <summary>
    /// Gets the execution-position envelope committed with the dispatch (DR-011a); null for
    /// kernel-level callers.
    /// </summary>
    public DurableExecutionEnvelope? Envelope { get; init; }

    /// <summary>
    /// Gets the stream version this command was decided against; the commit is rejected when
    /// the stream moved (DU-022).
    /// </summary>
    public StreamVersion? ExpectedStreamVersion { get; init; }

    /// <summary>
    /// Gets pending resumes whose matched events fed the step that produced this command;
    /// their consumption commits atomically with it (DR-011a).
    /// </summary>
    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];
}

/// <summary>
/// Records completion reported by an external work watcher.
/// </summary>
public sealed record CompleteExternalJobCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the provider-neutral external job identity.
    /// </summary>
    public required string ExternalJobId { get; init; }

    /// <summary>
    /// Gets the inbound completion event identity used for deduplication.
    /// </summary>
    public required EventId CompletionEventId { get; init; }
}

/// <summary>
/// Requests timeout handling for an external job.
/// </summary>
public sealed record TimeoutExternalJobCommand : WorkflowCommand
{
    /// <summary>
    /// Gets the provider-neutral external job identity.
    /// </summary>
    public required string ExternalJobId { get; init; }
}

/// <summary>
/// Records that a compensatable saga forward action completed successfully.
/// </summary>
public sealed record RecordSagaForwardActionCompletedCommand : WorkflowCommand
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
/// Records timeout handling for a saga forward action.
/// </summary>
public sealed record SagaForwardActionTimedOutCommand : WorkflowCommand
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
    /// Gets whether timeout policy requires compensating the scope.
    /// </summary>
    public required bool CompensateScope { get; init; }
}

/// <summary>
/// Requests durable compensation for a saga scope.
/// </summary>
public sealed record RequestSagaCompensationCommand : WorkflowCommand
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
/// Records successful completion of one saga compensating action.
/// </summary>
public sealed record CompleteSagaCompensationCommand : WorkflowCommand
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
/// Records failure of one saga compensating action.
/// </summary>
public sealed record FailSagaCompensationCommand : WorkflowCommand
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
public sealed record RecordSagaManualRecoveryCommand : WorkflowCommand
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
