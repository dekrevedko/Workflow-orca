using System.Diagnostics;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Walks a <see cref="WorkflowDefinition{TState}"/> body — <c>Init</c> (already applied by the
/// engine facade before this runs) → business steps, <c>If</c>/<c>While</c> control flow →
/// <c>End</c> — driving all orchestration decisions itself (CR-010/CR-012). Position is tracked
/// as a stack of <see cref="Frame"/>s (CR-015) so nested containers (If inside While, ...) and
/// rehydration are represented exactly. Only the <see cref="StepResult.Completed"/> and
/// <see cref="StepResult.Failed"/> variants are handled; <see cref="StepResult.WaitForEvent"/>
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
        if (instance.Pointer.Frames.Count == 0)
        {
            instance.Pointer = ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(0));
        }

        while (instance.Status == WorkflowStatus.Running)
        {
            var (sequence, isRoot) = ResolveCurrentSequence(definition.Root, instance.Pointer);
            var index = instance.Pointer.Frames[^1].SequenceIndex!.Value;

            if (index >= sequence.Steps.Count)
            {
                if (!TryPopToParent(instance))
                {
                    // Fell off the end of the root sequence with no explicit End — nothing
                    // further to do; the caller observes whatever status is current.
                    break;
                }

                continue;
            }

            var node = sequence.Steps[index];

            switch (node)
            {
                case EndNode endNode:
                    instance.EndOutcomeName = endNode.OutcomeName;
                    Advance(instance, LifecycleTrigger.Complete);
                    break;

                case BusinessStepNode<TState> businessStepNode:
                    await ExecuteBusinessNodeAsync(instance, businessStepNode, timeProvider, index, cancellationToken).ConfigureAwait(false);
                    if (instance.Status == WorkflowStatus.Running)
                    {
                        AdvanceSequenceIndex(instance);
                    }

                    break;

                case IfNode ifNode:
                    EnterIf(instance, ifNode);
                    break;

                case WhileNode whileNode:
                    EnterOrSkipWhile(instance, whileNode);
                    break;

                case DefinitionNode when isRoot && index == 0:
                    // Root position 0 is always Init when present (CR-005): the engine facade
                    // already converted input to state before this loop starts, so the
                    // interpreter treats this position as a no-op to advance past.
                    AdvanceSequenceIndex(instance);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Definition node '{node.GetType().Name}' is out of scope for the interpreter (T1-07).");
            }

            instance.UpdatedAt = timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Descends from <paramref name="root"/> following every frame pair but the trailing
    /// <see cref="Frame.SequenceIndex"/> frame, resolving the <see cref="SequenceNode"/> that
    /// trailing frame indexes into. Frames come in pairs per nesting level: a
    /// <see cref="Frame.SequenceIndex"/> frame naming the container's position in its enclosing
    /// sequence, followed by a <see cref="Frame.BranchId"/>/<see cref="Frame.LoopIteration"/>
    /// frame naming which child sequence of that container was entered (CR-015). Returns
    /// whether the resolved sequence is the definition root.
    /// </summary>
    private static (SequenceNode Sequence, bool IsRoot) ResolveCurrentSequence(SequenceNode root, ExecutionPointer pointer)
    {
        var sequence = root;
        var isRoot = true;

        for (var i = 0; i < pointer.Frames.Count - 1; i += 2)
        {
            var containerFrame = pointer.Frames[i];
            var childFrame = pointer.Frames[i + 1];
            var node = sequence.Steps[containerFrame.SequenceIndex!.Value];

            sequence = node switch
            {
                IfNode ifNode when childFrame.BranchId is { Name: "Then" } => ifNode.Then,
                IfNode ifNode when childFrame.BranchId is { Name: "Else" } => ifNode.Else,
                WhileNode whileNode when childFrame.LoopIteration is not null => whileNode.Body,
                _ => throw new UnreachableException($"Frame pair at depth {i} does not resolve to a nested sequence."),
            };
            isRoot = false;
        }

        return (sequence, isRoot);
    }

    /// <summary>Increments the innermost frame's sequence index in place, preserving outer frames.</summary>
    private static void AdvanceSequenceIndex(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        var innermost = frames[^1];
        var advanced = Frame.AtSequenceIndex(innermost.SequenceIndex!.Value + 1);

        instance.Pointer = ReplaceInnermost(instance.Pointer, advanced);
    }

    /// <summary>
    /// Pushes the appropriate branch frame (or skips straight past the If if both branches are
    /// empty) so the next loop iteration resolves into the chosen branch's sequence at index 0.
    /// </summary>
    private static void EnterIf(WorkflowInstance<TState> instance, IfNode ifNode)
    {
        var takeThen = ifNode.Condition(GetBoxedState(instance));
        var branchName = takeThen ? "Then" : "Else";

        instance.Pointer = instance.Pointer
            .Push(Frame.InBranch(new BranchId(takeThen ? 0 : 1, branchName)))
            .Push(Frame.AtSequenceIndex(0));
    }

    /// <summary>
    /// Evaluates the loop condition; if true, pushes a loop-body frame at index 0. If false,
    /// advances past the While node in the enclosing sequence.
    /// </summary>
    private static void EnterOrSkipWhile(WorkflowInstance<TState> instance, WhileNode whileNode)
    {
        if (whileNode.Condition(GetBoxedState(instance)))
        {
            instance.Pointer = instance.Pointer
                .Push(Frame.AtLoopIteration(0))
                .Push(Frame.AtSequenceIndex(0));
        }
        else
        {
            AdvanceSequenceIndex(instance);
        }
    }

    /// <summary>
    /// Called when a nested sequence (If branch or While body) has run past its last step. Pops
    /// back to the enclosing sequence and advances past the container that owned it — re-entering
    /// a While re-evaluates its condition (loop semantics); an If branch simply rejoins the
    /// enclosing sequence. Returns false if there is no parent to pop to (root exhausted).
    /// </summary>
    private static bool TryPopToParent(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        if (frames.Count <= 1)
        {
            return false;
        }

        var containerFrame = frames[^2];
        instance.Pointer = instance.Pointer.Pop().Pop();

        if (containerFrame.LoopIteration is not null)
        {
            // Loop body exhausted: leave the pointer at the While node's own index so the main
            // loop re-evaluates its condition on the next pass (re-entrant While semantics).
            return true;
        }

        // If branch exhausted: advance past the If node in the enclosing sequence.
        AdvanceSequenceIndex(instance);
        return true;
    }

    private static ExecutionPointer ReplaceInnermost(ExecutionPointer pointer, Frame replacement) =>
        pointer.Pop().Push(replacement);

    private static object? GetBoxedState(WorkflowInstance<TState> instance) => instance.State;

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
