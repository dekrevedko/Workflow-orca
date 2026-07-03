using System.Diagnostics;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Walks a <see cref="WorkflowDefinition{TState}"/> straight-line body — <c>Init</c> (already
/// applied by the engine facade before this runs) → business steps → <c>End</c> — driving all
/// orchestration decisions itself (CR-010/CR-012). Only the <see cref="StepResult.Completed"/>
/// and <see cref="StepResult.Failed"/> variants are handled; <see cref="StepResult.WaitForEvent"/>
/// and <see cref="StepResult.Yield"/> are out of scope until T1-08/T1-15.
/// </summary>
internal sealed class Interpreter<TState>
{
    /// <summary>
    /// Runs <paramref name="instance"/> against <paramref name="definition"/> from its current
    /// position to the next suspension or terminal (CR-016). All mutations route through this
    /// single method so the execution lane (T1-06/CR-040) can later wrap it.
    /// </summary>
    public async ValueTask RunAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var steps = definition.Root.Steps;
        var index = instance.Pointer.Frames.Count == 0 ? 0 : instance.Pointer.Frames[^1].SequenceIndex ?? 0;

        while (index < steps.Count && instance.Status == WorkflowStatus.Running)
        {
            var node = steps[index];
            instance.Pointer = ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(index));

            switch (node)
            {
                case EndNode endNode:
                    instance.EndOutcomeName = endNode.OutcomeName;
                    Advance(instance, LifecycleTrigger.Complete);
                    break;

                case BusinessStepNode<TState> businessStepNode:
                    await ExecuteBusinessNodeAsync(instance, businessStepNode, timeProvider, index, cancellationToken).ConfigureAwait(false);
                    break;

                case DefinitionNode when index == 0:
                    // Root position 0 is always Init when present (CR-005): the engine facade
                    // already converted input to state before this loop starts, so the
                    // interpreter treats this position as a no-op to advance past.
                    break;

                default:
                    throw new NotSupportedException(
                        $"Definition node '{node.GetType().Name}' is out of scope for the straight-line interpreter (T1-05).");
            }

            index++;
            instance.UpdatedAt = timeProvider.GetUtcNow();
        }
    }

    private async ValueTask ExecuteBusinessNodeAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> businessStepNode,
        TimeProvider timeProvider,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var step = businessStepNode.CreateStep();
        var context = new StepContext<TState>
        {
            State = instance.State,
            TimeProvider = timeProvider,
        };

        StepResult result;
        try
        {
            result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Fail(instance, DescribeError(exception.GetType().Name, exception.Message, stepIndex, timeProvider));
            return;
        }

        switch (result)
        {
            case StepResult.Completed:
                break;

            case StepResult.Failed failed:
                Fail(instance, DescribeError(failed.Error.GetType().Name, failed.Error.Message, stepIndex, timeProvider));
                break;

            case StepResult.WaitForEvent:
                throw new NotSupportedException(
                    "StepResult.WaitForEvent is not supported by the straight-line interpreter yet; waits land in T1-08.");

            case StepResult.Yield:
                throw new NotSupportedException(
                    "StepResult.Yield is not supported by the straight-line interpreter yet; yield lands in T1-15.");

            default:
                throw new UnreachableException($"Unhandled {nameof(StepResult)} variant '{result.GetType().Name}'.");
        }
    }

    private static void Fail(WorkflowInstance<TState> instance, string errorSummary)
    {
        instance.ErrorSummary = errorSummary;
        Advance(instance, LifecycleTrigger.Fail);
    }

    private static void Advance(WorkflowInstance<TState> instance, LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(instance.Status, trigger);
        instance.Status = result.IsSuccess ? result.Value : throw result.Error;
    }

    private static string DescribeError(string errorType, string message, int stepIndex, TimeProvider timeProvider) =>
        $"{errorType}: {message} (at root[{stepIndex}], {timeProvider.GetUtcNow():O})";
}
