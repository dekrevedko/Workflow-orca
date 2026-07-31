using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private StepTransition BlockForRetry(
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance,
        int attempt,
        TimeSpan backoff)
    {
        var obligationId = $"retry:{instruction.Id.Value}:{fiber.Id.Value}:{attempt}";
        var retryNotBefore = timeProvider.GetUtcNow().Add(backoff);
        var retryFiber = fiber with
        {
            RetryAttempt = attempt,
            RetryNotBefore = backoff > TimeSpan.Zero ? retryNotBefore : null
        };
        if (backoff == TimeSpan.Zero)
        {
            return new StepTransition(
                state with
                {
                    Fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
                    {
                        [fiber.Id] = retryFiber
                    },
                    Scheduler = FiberScheduler.CompleteTurn(
                        state.Scheduler,
                        fiber.Id,
                        requeueSelected: true)
                },
                InstanceTerminated: false);
        }

        var blocked = FiberReducer.Block(
            retryFiber,
            FiberBlockedReason.Retry,
            obligationId);
        var blockedState = state with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
            {
                [fiber.Id] = blocked
            },
            Scheduler = FiberScheduler.CompleteTurn(
                state.Scheduler,
                fiber.Id,
                requeueSelected: false)
        };
        _ = AwaitRetryAndResumeAsync(fiber.Id, obligationId, backoff, instance);
        return new StepTransition(blockedState, InstanceTerminated: false);
    }

    private async Task AwaitRetryAndResumeAsync(
        FiberId fiberId,
        string obligationId,
        TimeSpan backoff,
        WorkflowInstance<TState> instance)
    {
        try
        {
            using var instanceCancellation = instance.CreateLinkedExecutionToken(CancellationToken.None);
            if (backoff > TimeSpan.Zero)
            {
                await Task.Delay(backoff, timeProvider, instanceCancellation.Token).ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
            }

            await yieldContinuationScheduler.ResumeAsync(
                instance,
                instance.InstanceId,
                async cancellationToken =>
                {
                    var execution = activeExecution;
                    if (execution is null ||
                        !execution.Fibers.TryGetValue(fiberId, out var blocked) ||
                        blocked.Phase != FiberPhase.Blocked ||
                        blocked.Blocked != new FiberBlock(FiberBlockedReason.Retry, obligationId))
                    {
                        return;
                    }

                    activeExecution = execution with
                    {
                        Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                        {
                            [fiberId] = FiberReducer.Resume(blocked) with { RetryNotBefore = null }
                        },
                        Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, [fiberId])
                    };
                    await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
                },
                onSnapshotCommitted,
                instanceCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Instance or engine cancellation owns the terminal transition.
        }
        catch (Exception exception)
        {
            HandleBackgroundResumeFailure(
                instance,
                fiberId,
                $"{obligationId}:resume",
                exception);
        }
    }
}
