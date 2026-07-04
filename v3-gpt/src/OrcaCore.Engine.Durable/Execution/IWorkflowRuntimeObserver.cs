using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Observes durable runtime command outcomes for diagnostics and metrics.
/// </summary>
public interface IWorkflowRuntimeObserver
{
    /// <summary>
    /// Called after a durable command reaches a final outcome.
    /// </summary>
    ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes one durable command outcome at the runtime boundary.
/// </summary>
public sealed record WorkflowRuntimeObservation(
    WorkflowRuntimeObservationKind Kind,
    InstanceId InstanceId,
    DurableCommandOutcome Outcome,
    StreamVersion StreamVersion,
    int EventCount,
    bool CheckpointWritten,
    bool Evicted,
    EventId? InboxEventId,
    string? Message,
    string CommandType = "DurableCommand",
    DefinitionId? DefinitionId = null,
    DefinitionVersion? DefinitionVersion = null,
    WorkflowStatus? Status = null,
    TimeSpan Duration = default);

/// <summary>
/// Categorizes durable runtime observations without requiring callers to parse command result text.
/// </summary>
public enum WorkflowRuntimeObservationKind
{
    CommandCommitted,
    CommandConflict,
    CommandEvicted,
    CommandPoisoned,
    CommandNoOp
}

internal sealed class NullWorkflowRuntimeObserver : IWorkflowRuntimeObserver
{
    internal static NullWorkflowRuntimeObserver Instance { get; } = new();

    private NullWorkflowRuntimeObserver()
    {
    }

    public ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
