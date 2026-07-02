using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;

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
/// Requests cooperative cancellation for a durable workflow instance.
/// </summary>
public sealed record CancelWorkflowCommand : WorkflowCommand;

/// <summary>
/// Requests forced termination for a durable workflow instance.
/// </summary>
public sealed record TerminateWorkflowCommand : WorkflowCommand;
