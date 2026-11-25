using OrcaCore.Core.Lifecycle;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class SuspensionScheduler<TState>(
    TimeProvider timeProvider,
    EphemeralTimerService timerService)
{
    internal RuntimeTimerRecord RegisterStructuredDelay(
        WorkflowInstance<TState> instance,
        TimeSpan duration,
        BranchId? branchId,
        Func<CancellationToken, Task> resumeAsync)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(resumeAsync);
        if (instance.Status == global::OrcaCore.WorkflowInstanceStatus.Running)
        {
            WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var timer = instance.EnterDelay(branchId, timeProvider.GetUtcNow());
        var scheduledTimer = timerService.Schedule(
            instance.InstanceId,
            duration,
            cancellationToken => instance.FireDelayAsync(
                timer,
                timeProvider.GetUtcNow(),
                resumeAsync,
                cancellationToken));
        timer.SetCancel(() => timerService.Cancel(scheduledTimer));
        return timer;
    }

    internal void RegisterWorkflowDeadline(
        WorkflowInstance<TState> instance,
        DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var remaining = deadline - timeProvider.GetUtcNow();
        timerService.Schedule(
            instance.InstanceId,
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero,
            _ => Task.FromResult(instance.Timeout(deadline, timeProvider.GetUtcNow())));
    }

    internal void RegisterStructuredWaitTimeout(
        WorkflowInstance<TState> instance,
        RuntimeWaitRecord wait,
        TimeSpan timeout,
        Func<CancellationToken, Task> timeoutAsync)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(timeoutAsync);
        var scheduledTimer = timerService.Schedule(
            instance.InstanceId,
            timeout,
            cancellationToken => instance.FireWaitTimeoutAsync(
                wait,
                timeProvider.GetUtcNow(),
                timeoutAsync,
                cancellationToken));
        wait.SetCancelLoser(() => timerService.Cancel(scheduledTimer));
    }
}
