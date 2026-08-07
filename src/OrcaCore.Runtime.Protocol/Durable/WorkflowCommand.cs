using OrcaCore.Abstractions.Ids;
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
    /// Gets the parent workflow instance when this command starts child work.
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
    public required DefinitionId DefinitionId { get; init; }

    public required DefinitionVersion DefinitionVersion { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? DefinitionFingerprint { get; init; }

    public string? InputFingerprint { get; init; }

    /// <summary>
    /// Gets the content type of the serialized start input, when the driver starts the instance.
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
    public required DurableEventEnvelope Envelope { get; init; }

    public string? EnvelopeFingerprint { get; init; }
}

/// <summary>
/// Carries the serialized event data committed by the durable protocol.
/// </summary>
public sealed record DurableEventEnvelope
{
    public required EventId EventId { get; init; }

    public required string EventName { get; init; }

    /// <summary>Gets the positive application-owned event-contract version.</summary>
    public int EventContractVersion { get; init; } = 1;

    public required CorrelationId CorrelationId { get; init; }

    /// <summary>Gets the optional inbound event that caused this event.</summary>
    public EventId? CausationEventId { get; init; }

    public string? PayloadContentType { get; init; }

    public byte[]? Payload { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Gets the complete normalized routing intent accepted with the envelope.</summary>
    public required DurableEventRouteEnvelope Route { get; init; }
}

/// <summary>Provider-neutral persisted routing facts for one accepted inbound event.</summary>
public sealed record DurableEventRouteEnvelope
{
    public required string Kind { get; init; }

    public InstanceId? InstanceId { get; init; }

    public DefinitionId? DefinitionId { get; init; }

    public DefinitionVersion? DefinitionVersion { get; init; }

    public string? StartIdempotencyKey { get; init; }

    public string? WorkflowInputContentType { get; init; }

    public byte[]? WorkflowInputPayload { get; init; }
}

/// <summary>
/// Requests that a durable timer wake-up be recorded and scheduled.
/// </summary>
public sealed record ScheduleTimerCommand : WorkflowCommand
{
    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public required TimerId TimerId { get; init; }

    public required DateTimeOffset FireAt { get; init; }

    public required string WakeupName { get; init; }

    public DurableCheckpointPayload? Envelope { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }
}

/// <summary>
/// Requests that a due durable timer wake-up be applied to its workflow instance.
/// </summary>
public sealed record FireTimerCommand : WorkflowCommand
{
    public required TimerId TimerId { get; init; }
}

/// <summary>
/// Requests cooperative cancellation for a durable workflow instance.
/// </summary>
public sealed record CancelWorkflowCommand : WorkflowCommand;

/// <summary>
/// Requests forced termination for a durable workflow instance.
/// </summary>
public sealed record TerminateWorkflowCommand : WorkflowCommand;

/// <summary>
/// Requests durable history rollover while preserving the logical workflow instance identity.
/// </summary>
public sealed record ContinueAsNewCommand : WorkflowCommand
{
    public required string StateContentType { get; init; }

    public required byte[] StatePayload { get; init; }

    public DurableCheckpointPayload? Envelope { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }
}

/// <summary>
/// Requests durable acquisition of all resource-pool requirements for one guarded holder.
/// </summary>
public sealed record AcquireResourcePoolCommand : WorkflowCommand
{
    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }

    public required string HolderKey { get; init; }

    public required IReadOnlyList<ResourcePoolRequirement> Requirements { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public WaitId? WaitId { get; init; }

    public DurableCheckpointPayload? Envelope { get; init; }

    public StreamVersion? ExpectedStreamVersion { get; init; }

    public IReadOnlyList<WaitId> ConsumedResumeWaitIds { get; init; } = [];

    public string? LeaseObligationId { get; init; }

    public string? LeaseProtectionToken { get; init; }

    public int LeaseGeneration { get; init; }

    public string? LeaseFiberOccurrence { get; init; }

    public string? LeaseScopeOccurrence { get; init; }
}
