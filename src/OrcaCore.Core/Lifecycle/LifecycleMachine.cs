using System.Collections.Frozen;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Lifecycle;

internal static class LifecycleMachine
{
    public static readonly FrozenDictionary<LifecycleTransition, global::OrcaCore.WorkflowInstanceStatus> Transitions =
        new Dictionary<LifecycleTransition, global::OrcaCore.WorkflowInstanceStatus>
        {
            [new(global::OrcaCore.WorkflowInstanceStatus.Pending, LifecycleTrigger.Start)] = global::OrcaCore.WorkflowInstanceStatus.Running,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.EnterWait)] = global::OrcaCore.WorkflowInstanceStatus.Waiting,
            [new(global::OrcaCore.WorkflowInstanceStatus.Waiting, LifecycleTrigger.MatchWait)] = global::OrcaCore.WorkflowInstanceStatus.Running,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Complete)] = global::OrcaCore.WorkflowInstanceStatus.Completed,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Fail)] = global::OrcaCore.WorkflowInstanceStatus.Failed,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Cancel)] = global::OrcaCore.WorkflowInstanceStatus.Cancelled,
            [new(global::OrcaCore.WorkflowInstanceStatus.Waiting, LifecycleTrigger.Cancel)] = global::OrcaCore.WorkflowInstanceStatus.Cancelled,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Terminate)] = global::OrcaCore.WorkflowInstanceStatus.Terminated,
            [new(global::OrcaCore.WorkflowInstanceStatus.Waiting, LifecycleTrigger.Terminate)] = global::OrcaCore.WorkflowInstanceStatus.Terminated,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Park)] = global::OrcaCore.WorkflowInstanceStatus.Waiting,
            [new(global::OrcaCore.WorkflowInstanceStatus.Waiting, LifecycleTrigger.Unpark)] = global::OrcaCore.WorkflowInstanceStatus.Running,
            [new(global::OrcaCore.WorkflowInstanceStatus.CancellationRequested, LifecycleTrigger.Cancel)] = global::OrcaCore.WorkflowInstanceStatus.Cancelled,
            [new(global::OrcaCore.WorkflowInstanceStatus.CancellationRequested, LifecycleTrigger.Terminate)] = global::OrcaCore.WorkflowInstanceStatus.Terminated,
            [new(global::OrcaCore.WorkflowInstanceStatus.Running, LifecycleTrigger.Timeout)] = global::OrcaCore.WorkflowInstanceStatus.TimedOut,
            [new(global::OrcaCore.WorkflowInstanceStatus.Waiting, LifecycleTrigger.Timeout)] = global::OrcaCore.WorkflowInstanceStatus.TimedOut
        }.ToFrozenDictionary();

    public static readonly FrozenSet<global::OrcaCore.WorkflowInstanceStatus> TerminalStatuses =
        new[]
        {
            global::OrcaCore.WorkflowInstanceStatus.Completed,
            global::OrcaCore.WorkflowInstanceStatus.Failed,
            global::OrcaCore.WorkflowInstanceStatus.Cancelled,
            global::OrcaCore.WorkflowInstanceStatus.Terminated,
            global::OrcaCore.WorkflowInstanceStatus.TimedOut
        }.ToFrozenSet();

    public static Result<global::OrcaCore.WorkflowInstanceStatus> Fire(
        global::OrcaCore.WorkflowInstanceStatus current,
        LifecycleTrigger trigger)
    {
        var transition = new LifecycleTransition(current, trigger);
        if (Transitions.TryGetValue(transition, out var target))
        {
            return Result<global::OrcaCore.WorkflowInstanceStatus>.Success(target);
        }

        return Result<global::OrcaCore.WorkflowInstanceStatus>.Failure(
            new WorkflowLifecycleException(
                $"Lifecycle trigger '{trigger}' is not valid from workflow status '{current}'."));
    }
}
