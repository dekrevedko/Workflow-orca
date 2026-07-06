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
    TimeSpan Duration = default)
{
    public IReadOnlyList<WorkflowRuntimeEventObservation> Events { get; init; } = [];

    public bool InboxDuplicate { get; init; }

    public bool ProviderCommitAttempted { get; init; }

    public string ProviderName { get; init; } = "unknown";

    public string ProviderOperation { get; init; } = "append";

    public TimeSpan ProviderCommitDuration { get; init; }
}

/// <summary>
/// Describes a telemetry-safe durable event summary.
/// <see cref="StepDuration"/> measures processing of the step-completion command inside the durable
/// kernel (decide + commit), not the wall time of the business step itself — step code executes
/// outside the kernel, which never observes when it started.
/// </summary>
public sealed record WorkflowRuntimeEventObservation(
    string EventType,
    DefinitionId? DefinitionId = null,
    string? StepPath = null,
    string? ErrorKind = null,
    string? LifecycleEventName = null,
    bool DurableLifecycle = true,
    string? WaitEventName = null,
    TimeSpan? WaitDuration = null,
    TimeSpan? StepDuration = null);

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
