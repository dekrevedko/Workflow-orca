using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Straight-line interpreter (T1-05): walks <c>Init → business steps → End</c>, owning every
/// orchestration decision (CR-010). Steps are passive: they execute and return
/// <see cref="StepResult"/>; the interpreter alone advances position and lifecycle status.
/// Nested containers (If/While/Parallel), waits, and Yield are out of scope here and surface
/// as <see cref="NotSupportedException"/> naming the task that owns them.
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
    /// (CR-016). Only <c>Init</c>, business steps, and <c>End</c> at the root sequence are
    /// supported; any other node type throws <see cref="NotSupportedException"/>.
    /// </summary>
    internal async ValueTask<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var steps = definition.Root.Steps;
        WorkflowInstance<TState>? instance = null;

        for (var index = 0; index < steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"root[{index}]";

            switch (steps[index])
            {
                case InitNode<TState, TInput> initNode:
                    instance = CreateInstance(initNode, input, definition, instanceId, index);
                    break;

                case BusinessStepNode<TState> stepNode:
                    var running = RequireStarted(instance, path);
                    await ExecuteStepAsync(running, stepNode, path, index, cancellationToken).ConfigureAwait(false);
                    if (running.Status != WorkflowStatus.Running)
                    {
                        return running;
                    }

                    break;

                case EndNode endNode:
                    var completing = RequireStarted(instance, path);
                    Complete(completing, endNode.OutcomeName, index);
                    return completing;

                default:
                    throw new NotSupportedException(
                        $"Definition node '{steps[index].GetType().Name}' at '{path}' is out of scope for the " +
                        "straight-line interpreter (T1-05); nested containers arrive in T1-07/T1-12.");
            }
        }

        return RequireStarted(instance, "root");
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
                instance.Advance(PointerAt(index), WorkflowStatus.Running, timeProvider.GetUtcNow());
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
