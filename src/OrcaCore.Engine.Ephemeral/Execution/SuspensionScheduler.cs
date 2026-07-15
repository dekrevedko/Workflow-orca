using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class SuspensionScheduler<TState>(
    TimeProvider timeProvider,
    EphemeralTimerService timerService)
{
    internal async Task RegisterWaitAsync<TInput>(
        WorkflowInstance<TState> instance,
        string eventName,
        CorrelationId correlationId,
        TimeSpan? timeout,
        SequenceExecutionContext<TState, TInput> context,
        int nextIndex,
        ISequenceExecutionEngine<TState> sequenceExecution,
        CancellationToken cancellationToken)
    {
        if (instance.Status == WorkflowStatus.Running)
        {
            WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var wait = instance.EnterWait(
            eventName,
            correlationId,
            context.BranchId,
            timeProvider.GetUtcNow(),
            (envelope, resumeToken) => sequenceExecution.ContinueSequenceAsync(
                context with { ResumeEvent = new ResumeEventSlot(envelope) },
                nextIndex,
                resumeToken));
        if (timeout is { } timeoutDuration)
        {
            var timeoutTimer = timerService.Schedule(
                context.InstanceId,
                timeoutDuration,
                timerCancellationToken => instance.FireWaitTimeoutAsync(
                    wait,
                    timeProvider.GetUtcNow(),
                    continuationToken => sequenceExecution.ContinueSequenceAsync(
                        context with { ResumeEvent = new ResumeEventSlot(null) },
                        nextIndex,
                        continuationToken),
                    timerCancellationToken));
            wait.SetCancelLoser(() => timerService.Cancel(timeoutTimer));
        }

        await instance.MatchPendingEventAsync(wait, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }

    internal void RegisterDelay<TInput>(
        WorkflowInstance<TState> instance,
        TimeSpan duration,
        SequenceExecutionContext<TState, TInput> context,
        int nextIndex,
        ISequenceExecutionEngine<TState> sequenceExecution)
    {
        if (instance.Status == WorkflowStatus.Running)
        {
            WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var registeredAt = timeProvider.GetUtcNow();
        var timer = instance.EnterDelay(context.BranchId, registeredAt);
        var scheduledTimer = timerService.Schedule(
            context.InstanceId,
            duration,
            cancellationToken => instance.FireDelayAsync(
                timer,
                timeProvider.GetUtcNow(),
                continuationToken => sequenceExecution.ContinueSequenceAsync(
                    context,
                    nextIndex,
                    continuationToken),
                cancellationToken));
        timer.SetCancel(() => timerService.Cancel(scheduledTimer));
    }

    internal RuntimeTimerRecord RegisterStructuredDelay(
        WorkflowInstance<TState> instance,
        TimeSpan duration,
        BranchId? branchId,
        Func<CancellationToken, Task> resumeAsync)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(resumeAsync);
        if (instance.Status == WorkflowStatus.Running)
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
}
