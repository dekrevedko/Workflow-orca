using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
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
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var advanced = Advance(plan, state, fiber, instruction);
        var advancedFiber = advanced.Fibers[fiber.Id];
        if (instance.Status == global::OrcaCore.WorkflowInstanceStatus.Running)
        {
            WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var registeredAt = timeProvider.GetUtcNow();
        var deadline = instruction.WaitTimeout.HasValue
            ? registeredAt.Add(instruction.WaitTimeout.Value)
            : (DateTimeOffset?)null;
        var runtimeWait = instance.EnterWait(
            eventContract,
            correlationId,
            BranchIdForFiber(plan, state, fiber),
            registeredAt,
            instruction.Path,
            deadline,
            (matched, resumeToken) => ResumeFiberAsync(fiber.Id, matched, resumeToken),
            fiber.Id,
            fiber.OwningScopeId);
        waitsByFiber[fiber.Id] = runtimeWait;
        if (instruction.WaitTimeout is { } waitTimeout)
        {
            suspensionScheduler.RegisterStructuredWaitTimeout(
                instance,
                runtimeWait,
                waitTimeout,
                timeoutToken => FailTimedOutWaitAsync(
                    fiber.Id,
                    instruction,
                    eventContract,
                    correlationId,
                    timeoutToken));
        }

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

    private async Task FailTimedOutWaitAsync(
        FiberId fiberId,
        CompiledInstruction instruction,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var execution = activeExecution ??
            throw new InvalidOperationException("The structured execution state is not initialized.");
        var instance = activeInstance ??
            throw new InvalidOperationException("The workflow instance is not initialized.");
        if (!execution.Fibers.TryGetValue(fiberId, out var fiber) ||
            fiber.Phase != FiberPhase.Blocked)
        {
            return;
        }

        waitsByFiber.Remove(fiberId);
        var exception = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.WaitTimeout(
            eventContract,
            correlationId);
        var failure = CreateFiberFailure(execution, fiber, instruction, exception);
        activeExecution = FailFiberAndAncestors(execution, fiber, failure);
        if (activeExecution.Fibers[activeExecution.RootFiberId].Phase == FiberPhase.Failed)
        {
            FailInstance(instance, failure, instruction.Path);
            return;
        }

        await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
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
