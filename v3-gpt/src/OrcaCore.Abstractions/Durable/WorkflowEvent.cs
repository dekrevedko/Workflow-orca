using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

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
/// Records that an inbound delivery was buffered while an instance was paused.
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
