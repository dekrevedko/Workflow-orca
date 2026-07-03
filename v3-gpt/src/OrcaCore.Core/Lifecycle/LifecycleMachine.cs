using System.Collections.Frozen;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Lifecycle;

internal static class LifecycleMachine
{
    internal static readonly FrozenDictionary<LifecycleTransition, WorkflowStatus> Transitions =
        new Dictionary<LifecycleTransition, WorkflowStatus>
        {
            [new(WorkflowStatus.Running, LifecycleTrigger.EnterWait)] = WorkflowStatus.Waiting,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.MatchWait)] = WorkflowStatus.Running,
            [new(WorkflowStatus.Running, LifecycleTrigger.Complete)] = WorkflowStatus.Completed,
            [new(WorkflowStatus.Running, LifecycleTrigger.Fail)] = WorkflowStatus.Failed,
            [new(WorkflowStatus.Running, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled,
            [new(WorkflowStatus.Running, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [new(WorkflowStatus.Running, LifecycleTrigger.Compensate)] = WorkflowStatus.Compensated,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.Compensate)] = WorkflowStatus.Compensated,
            [new(WorkflowStatus.Running, LifecycleTrigger.FailCompensation)] = WorkflowStatus.CompensationFailed,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.FailCompensation)] = WorkflowStatus.CompensationFailed,
            [new(WorkflowStatus.Running, LifecycleTrigger.Pause)] = WorkflowStatus.Paused,
            [new(WorkflowStatus.Waiting, LifecycleTrigger.Pause)] = WorkflowStatus.Paused,
            [new(WorkflowStatus.Paused, LifecycleTrigger.Resume)] = WorkflowStatus.Running,
            [new(WorkflowStatus.Paused, LifecycleTrigger.Terminate)] = WorkflowStatus.Terminated,
            [new(WorkflowStatus.Paused, LifecycleTrigger.Cancel)] = WorkflowStatus.Cancelled
        }.ToFrozenDictionary();

    internal static readonly FrozenSet<WorkflowStatus> TerminalStatuses =
        new[]
        {
            WorkflowStatus.Completed,
            WorkflowStatus.Failed,
            WorkflowStatus.Cancelled,
            WorkflowStatus.Terminated,
            WorkflowStatus.Compensated,
            WorkflowStatus.CompensationFailed
        }.ToFrozenSet();

    internal static Result<WorkflowStatus> Fire(WorkflowStatus current, LifecycleTrigger trigger)
    {
        var transition = new LifecycleTransition(current, trigger);
        if (Transitions.TryGetValue(transition, out var target))
        {
            return Result<WorkflowStatus>.Success(target);
        }

        return Result<WorkflowStatus>.Failure(
            new WorkflowLifecycleException(
                $"Lifecycle trigger '{trigger}' is not valid from workflow status '{current}'."));
    }
}
