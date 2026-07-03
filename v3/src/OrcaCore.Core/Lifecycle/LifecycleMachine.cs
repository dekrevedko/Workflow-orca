using System.Collections.Frozen;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Lifecycle;

/// <summary>
/// Pure, stateless instance lifecycle machine (CR-030). The transition table is data, not
/// control flow, and covers every <see cref="WorkflowStatus"/> as either a legal source or a
/// declared terminal. Public so both engine assemblies can drive it (Core is engine-agnostic
/// shared runtime machinery, not itself part of the authoring-facing API).
/// </summary>
public static class LifecycleMachine
{
    private static readonly FrozenDictionary<(WorkflowStatus Current, LifecycleTrigger Trigger), WorkflowStatus> Transitions =
        new Dictionary<(WorkflowStatus, LifecycleTrigger), WorkflowStatus>
        {
            [(WorkflowStatus.Running, LifecycleTrigger.EnterWait)] = WorkflowStatus.Waiting,
            [(WorkflowStatus.Waiting, LifecycleTrigger.MatchWait)] = WorkflowStatus.Running,
            [(WorkflowStatus.Running, LifecycleTrigger.Complete)] = WorkflowStatus.Completed,
            [(WorkflowStatus.Running, LifecycleTrigger.Fail)] = WorkflowStatus.Failed,
            [(WorkflowStatus.Running, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled,
            [(WorkflowStatus.Waiting, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled,
            [(WorkflowStatus.Running, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [(WorkflowStatus.Waiting, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [(WorkflowStatus.Running, LifecycleTrigger.Pause)] = WorkflowStatus.Paused,
            [(WorkflowStatus.Waiting, LifecycleTrigger.Pause)] = WorkflowStatus.Paused,
            [(WorkflowStatus.Paused, LifecycleTrigger.Resume)] = WorkflowStatus.Running,
            [(WorkflowStatus.Paused, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [(WorkflowStatus.Paused, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled,
        }.ToFrozenDictionary();

    /// <summary>Terminal statuses reject every trigger (CR-030); <c>Paused</c> is not terminal.</summary>
    public static readonly FrozenSet<WorkflowStatus> TerminalStatuses = new[]
    {
        WorkflowStatus.Completed,
        WorkflowStatus.Failed,
        WorkflowStatus.Cancelled,
        WorkflowStatus.Terminated,
    }.ToFrozenSet();

    public static Result<WorkflowStatus> Fire(WorkflowStatus current, LifecycleTrigger trigger) =>
        Transitions.TryGetValue((current, trigger), out var next)
            ? Result<WorkflowStatus>.Success(next)
            : Result<WorkflowStatus>.Failure(new WorkflowLifecycleException(
                $"Illegal lifecycle trigger '{trigger}' from status '{current}'."));
}
