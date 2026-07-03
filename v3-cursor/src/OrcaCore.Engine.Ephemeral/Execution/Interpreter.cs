using System.Diagnostics;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Ephemeral interpreter (T1-05/T1-07): walks <c>Init → business steps → If/While → End</c>,
/// owning every orchestration decision (CR-010). Steps are passive: they execute and return
/// <see cref="StepResult"/>; the interpreter alone advances position and lifecycle status.
/// Position is tracked as a stack of <see cref="Frame"/>s (CR-015). Waits, Yield, and
/// <see cref="ParallelNode"/> are out of scope and surface as <see cref="NotSupportedException"/>.
/// </summary>
/// <typeparam name="TState">Workflow-owned business state type.</typeparam>
internal sealed class Interpreter<TState>
{
    private readonly TimeProvider timeProvider;

    internal Interpreter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Runs <paramref name="definition"/> to its first suspension or terminal outcome, inline
    /// (CR-016).
    /// </summary>
    internal async ValueTask<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        WorkflowInstance<TState>? instance = null;
        var pointer = ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(0));

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (instance is not null && instance.Status != WorkflowStatus.Running)
            {
                return instance;
            }

            var (sequence, _) = ResolveCurrentSequence(definition.Root, pointer);
            var index = pointer.Frames[^1].SequenceIndex!.Value;
            var path = BuildPath(definition.Root, pointer);

            if (index >= sequence.Steps.Count)
            {
                if (instance is null || !TryPopToParent(instance))
                {
                    return RequireStarted(instance, path);
                }

                pointer = instance.Pointer;
                continue;
            }

            switch (sequence.Steps[index])
            {
                case InitNode<TState, TInput> initNode:
                    if (instance is not null)
                    {
                        throw new WorkflowDefinitionException(
                            $"Workflow execution reached a second Init step at '{path}'; only one Init is allowed.");
                    }

                    instance = CreateInstance(initNode, input, definition, instanceId, index);
                    AdvanceSequenceIndex(instance);
                    pointer = instance.Pointer;
                    break;

                case BusinessStepNode<TState> stepNode:
                    var running = RequireStarted(instance, path);
                    await ExecuteStepAsync(running, stepNode, path, index, cancellationToken).ConfigureAwait(false);
                    pointer = running.Pointer;
                    if (running.Status != WorkflowStatus.Running)
                    {
                        return running;
                    }

                    AdvanceSequenceIndex(running);
                    pointer = running.Pointer;
                    break;

                case EndNode endNode:
                    var completing = RequireStarted(instance, path);
                    Complete(completing, endNode.OutcomeName, index);
                    return completing;

                case IfNode ifNode:
                    var ifInstance = RequireStarted(instance, path);
                    if (!TryEnterIf(ifInstance, ifNode, path, index))
                    {
                        return ifInstance;
                    }

                    pointer = ifInstance.Pointer;
                    break;

                case WhileNode whileNode:
                    var whileInstance = RequireStarted(instance, path);
                    if (!TryEnterOrSkipWhile(whileInstance, whileNode, path, index))
                    {
                        return whileInstance;
                    }

                    pointer = whileInstance.Pointer;
                    break;

                default:
                    throw new NotSupportedException(
                        $"Definition node '{sequence.Steps[index].GetType().Name}' at '{path}' is out of scope for the " +
                        "ephemeral interpreter; Parallel arrives in T1-12, Wait in T1-08.");
            }
        }
    }

    private WorkflowInstance<TState> CreateInstance<TInput>(
        InitNode<TState, TInput> initNode,
        TInput input,
        WorkflowDefinition<TState> definition,
        InstanceId instanceId,
        int index)
    {
        var now = timeProvider.GetUtcNow();
        var state = initNode.CreateState(input);
        var instance = new WorkflowInstance<TState>(instanceId, definition.DefinitionId, definition.DefinitionVersion, state, now);
        instance.Advance(PointerAt(index), WorkflowStatus.Running, now);
        return instance;
    }

    private async ValueTask ExecuteStepAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> stepNode,
        string path,
        int index,
        CancellationToken cancellationToken)
    {
        var step = stepNode.CreateStep();
        var context = new StepContext<TState>(instance.State, timeProvider);

        StepResult result;
        try
        {
            result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Fail(instance, exception, path, index);
            return;
        }

        switch (result)
        {
            case StepResult.Completed:
                instance.Advance(instance.Pointer, WorkflowStatus.Running, timeProvider.GetUtcNow());
                return;

            case StepResult.Failed failed:
                Fail(instance, failed.Error, path, index);
                return;

            case StepResult.WaitForEvent:
                throw new NotSupportedException(
                    $"StepResult.WaitForEvent at '{path}' is not supported yet; owned by T1-08 " +
                    "(resident waits and instance-targeted matching).");

            case StepResult.Yield:
                throw new NotSupportedException(
                    $"StepResult.Yield at '{path}' is not supported yet; owned by T1-15 (Yield continuation).");

            default:
                throw new NotSupportedException($"StepResult '{result.GetType().Name}' at '{path}' is not recognized.");
        }
    }

    private bool TryEnterIf(WorkflowInstance<TState> instance, IfNode ifNode, string path, int index)
    {
        bool takeThen;
        try
        {
            takeThen = ifNode.Condition(instance.State);
        }
        catch (Exception exception)
        {
            Fail(instance, exception, path, index);
            return false;
        }

        var branchName = takeThen ? "Then" : "Else";
        var pointer = instance.Pointer
            .Push(Frame.InBranch(new BranchId(takeThen ? 0 : 1, branchName)))
            .Push(Frame.AtSequenceIndex(0));

        instance.Advance(pointer, WorkflowStatus.Running, timeProvider.GetUtcNow());
        return true;
    }

    private bool TryEnterOrSkipWhile(WorkflowInstance<TState> instance, WhileNode whileNode, string path, int index)
    {
        bool continueLoop;
        try
        {
            continueLoop = whileNode.Condition(instance.State);
        }
        catch (Exception exception)
        {
            Fail(instance, exception, path, index);
            return false;
        }

        if (continueLoop)
        {
            var iteration = instance.LoopIterationCounters.GetValueOrDefault(whileNode);
            instance.LoopIterationCounters[whileNode] = iteration + 1;

            var pointer = instance.Pointer
                .Push(Frame.AtLoopIteration(iteration))
                .Push(Frame.AtSequenceIndex(0));

            instance.Advance(pointer, WorkflowStatus.Running, timeProvider.GetUtcNow());
        }
        else
        {
            AdvanceSequenceIndex(instance);
        }

        return true;
    }

    private bool TryPopToParent(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        if (frames.Count <= 1)
        {
            return false;
        }

        var containerFrame = frames[^2];
        var pointer = instance.Pointer.Pop().Pop();
        var now = timeProvider.GetUtcNow();

        if (containerFrame.LoopIteration is not null)
        {
            instance.Advance(pointer, WorkflowStatus.Running, now);
            return true;
        }

        instance.Advance(pointer, WorkflowStatus.Running, now);
        AdvanceSequenceIndex(instance);
        return true;
    }

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

    private void AdvanceSequenceIndex(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        var innermost = frames[^1];
        var advanced = Frame.AtSequenceIndex(innermost.SequenceIndex!.Value + 1);
        instance.Advance(ReplaceInnermost(instance.Pointer, advanced), WorkflowStatus.Running, timeProvider.GetUtcNow());
    }

    private static ExecutionPointer ReplaceInnermost(ExecutionPointer pointer, Frame replacement) =>
        pointer.Pop().Push(replacement);

    private static string BuildPath(SequenceNode root, ExecutionPointer pointer)
    {
        if (pointer.Frames.Count == 0)
        {
            return "root";
        }

        var segments = new List<string>();
        var sequence = root;

        for (var i = 0; i < pointer.Frames.Count; i++)
        {
            var frame = pointer.Frames[i];
            if (frame.SequenceIndex is { } sequenceIndex)
            {
                var prefix = ReferenceEquals(sequence, root) ? "root" : "sequence";
                segments.Add($"{prefix}[{sequenceIndex}]");
                if (sequenceIndex < sequence.Steps.Count)
                {
                    var node = sequence.Steps[sequenceIndex];
                    if (i + 1 < pointer.Frames.Count)
                    {
                        var childFrame = pointer.Frames[i + 1];
                        sequence = node switch
                        {
                            IfNode ifNode when childFrame.BranchId is { Name: "Then" } => ifNode.Then,
                            IfNode ifNode when childFrame.BranchId is { Name: "Else" } => ifNode.Else,
                            WhileNode whileNode when childFrame.LoopIteration is not null =>
                                whileNode.Body,
                            _ => sequence,
                        };

                        if (childFrame.BranchId is { Name: var branchName })
                        {
                            segments[^1] += $"/{branchName}";
                            i++;
                        }
                        else if (childFrame.LoopIteration is { } loopIteration)
                        {
                            segments[^1] += $"/iter{loopIteration}";
                            i++;
                        }
                    }
                }
            }
        }

        return string.Join("/", segments);
    }

    private void Fail(WorkflowInstance<TState> instance, Exception exception, string path, int index)
    {
        var now = timeProvider.GetUtcNow();
        var transition = LifecycleMachine.Fire(instance.Status, LifecycleTrigger.Fail);
        if (transition.IsFailure)
        {
            throw transition.Error;
        }

        var error = new StepErrorDetails(exception.GetType().Name, exception.Message, path, now);
        instance.Advance(PointerAt(index), transition.Value, now, error: error);
    }

    private void Complete(WorkflowInstance<TState> instance, string? outcomeName, int index)
    {
        var now = timeProvider.GetUtcNow();
        var transition = LifecycleMachine.Fire(instance.Status, LifecycleTrigger.Complete);
        if (transition.IsFailure)
        {
            throw transition.Error;
        }

        instance.Advance(PointerAt(index), transition.Value, now, endOutcomeName: outcomeName);
    }

    private static ExecutionPointer PointerAt(int index) => ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(index));

    private static WorkflowInstance<TState> RequireStarted(WorkflowInstance<TState>? instance, string path) =>
        instance ?? throw new WorkflowDefinitionException(
            $"Workflow execution reached '{path}' before an Init step created business state.");
}
