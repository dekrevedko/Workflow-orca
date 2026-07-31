using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Ephemeral.Governance;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private StepTransition BlockForResourceGrant(
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance)
    {
        var obligationId = $"transient:{instruction.Policy.TransientPoolKey ?? "global"}:{fiber.Id.Value}";
        var blockedFiber = FiberReducer.Block(fiber, FiberBlockedReason.Resource, obligationId);
        var blockedState = state with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
            {
                [fiber.Id] = blockedFiber
            },
            Scheduler = FiberScheduler.CompleteTurn(
                state.Scheduler,
                fiber.Id,
                requeueSelected: false)
        };
        _ = AwaitResourceGrantAndResumeAsync(
            fiber.Id,
            obligationId,
            instruction.StepType,
            instruction.Policy.TransientPoolKey,
            instance);
        return new StepTransition(blockedState, InstanceTerminated: false);
    }

    private async Task AwaitResourceGrantAndResumeAsync(
        FiberId fiberId,
        string obligationId,
        Type? exactStepType,
        string? poolKey,
        WorkflowInstance<TState> instance)
    {
        GovernanceLease? lease = null;
        try
        {
            using var instanceCancellation = instance.CreateLinkedExecutionToken(CancellationToken.None);
            lease = await governance.EnterStepAsync(
                exactStepType,
                poolKey,
                instanceCancellation.Token).ConfigureAwait(false);
            await yieldContinuationScheduler.ResumeAsync(
                instance,
                instance.InstanceId,
                async cancellationToken =>
                {
                    var execution = activeExecution;
                    if (execution is null ||
                        !execution.Fibers.TryGetValue(fiberId, out var blocked) ||
                        blocked.Phase != FiberPhase.Blocked ||
                        blocked.Blocked != new FiberBlock(FiberBlockedReason.Resource, obligationId))
                    {
                        return;
                    }

                    grantedStepLeases[fiberId] = lease;
                    lease = null;
                    activeExecution = execution with
                    {
                        Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                        {
                            [fiberId] = FiberReducer.Resume(blocked)
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
        finally
        {
            try
            {
                if (lease is not null)
                {
                    await lease.DisposeAsync().ConfigureAwait(false);
                }

                if (grantedStepLeases.Remove(fiberId, out var unconsumed))
                {
                    await unconsumed.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                HandleBackgroundResumeFailure(
                    instance,
                    fiberId,
                    $"{obligationId}:release",
                    exception);
            }
        }
    }
}
