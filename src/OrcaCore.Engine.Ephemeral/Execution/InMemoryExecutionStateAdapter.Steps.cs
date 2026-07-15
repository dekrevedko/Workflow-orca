using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private async Task<StructuredExecutionState> RegisterFiberWaitAsync(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance,
        string eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var advanced = Advance(plan, state, fiber, instruction);
        var advancedFiber = advanced.Fibers[fiber.Id];
        if (instance.Status == WorkflowStatus.Running)
        {
            WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var runtimeWait = instance.EnterWait(
            eventName,
            correlationId,
            BranchIdForFiber(plan, state, fiber),
            timeProvider.GetUtcNow(),
            (matched, resumeToken) => ResumeFiberAsync(fiber.Id, matched, resumeToken),
            fiber.Id,
            fiber.OwningScopeId);
        waitsByFiber[fiber.Id] = runtimeWait;
        var blockedFibers = new Dictionary<FiberId, FiberRecord>(advanced.Fibers)
        {
            [fiber.Id] = FiberReducer.Block(
                advancedFiber,
                FiberBlockedReason.Wait,
                runtimeWait.WaitId.ToString())
        };
        activeExecution = advanced with
        {
            Fibers = blockedFibers,
            Scheduler = FiberScheduler.CompleteTurn(
                advanced.Scheduler,
                fiber.Id,
                requeueSelected: false)
        };
        await instance.MatchPendingEventAsync(
            runtimeWait,
            timeProvider.GetUtcNow(),
            cancellationToken).ConfigureAwait(false);
        return activeExecution;
    }

    private StructuredExecutionState RegisterFiberDelay(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance,
        TimeSpan duration)
    {
        var advanced = Advance(plan, state, fiber, instruction);
        var advancedFiber = advanced.Fibers[fiber.Id];
        var timer = suspensionScheduler.RegisterStructuredDelay(
            instance,
            duration,
            BranchIdForFiber(plan, state, fiber),
            cancellationToken => ResumeTimerFiberAsync(fiber.Id, cancellationToken));
        timersByFiber[fiber.Id] = timer;
        return advanced with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(advanced.Fibers)
            {
                [fiber.Id] = FiberReducer.Block(
                    advancedFiber,
                    FiberBlockedReason.Timer,
                    timer.TimerId.ToString())
            },
            Scheduler = FiberScheduler.CompleteTurn(
                advanced.Scheduler,
                fiber.Id,
                requeueSelected: false)
        };
    }
}
