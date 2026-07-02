using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class Interpreter<TState>
{
    private readonly TimeProvider timeProvider;

    internal Interpreter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.timeProvider = timeProvider;
    }

    internal async Task<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var state = default(TState);
        var initialized = false;
        WorkflowInstance<TState>? instance = null;

        foreach (var node in definition.RootSequence.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case InitNode<TState> initNode:
                    state = initNode.CreateState(input);
                    initialized = true;
                    instance = new WorkflowInstance<TState>(
                        instanceId,
                        definition.DefinitionId,
                        definition.DefinitionVersion,
                        state,
                        timeProvider.GetUtcNow());
                    break;
                case BusinessStepNode<TState> stepNode:
                    EnsureInitialized(initialized, instance);
                    var shouldContinue = await ExecuteStepAsync(
                        instance!,
                        stepNode,
                        node.NodeId,
                        cancellationToken).ConfigureAwait(false);
                    if (!shouldContinue)
                    {
                        return instance!;
                    }

                    break;
                case EndNode<TState> endNode:
                    EnsureInitialized(initialized, instance);
                    FireOrThrow(instance!, LifecycleTrigger.Complete);
                    instance!.Complete(endNode.OutcomeName, timeProvider.GetUtcNow());
                    return instance;
                case IfNode<TState>:
                    throw new NotSupportedException("If interpretation is owned by T1-07.");
                case WhileNode<TState>:
                    throw new NotSupportedException("While interpretation is owned by T1-07.");
                case ParallelNode<TState>:
                    throw new NotSupportedException("Parallel interpretation is owned by T1-12.");
                case WaitNode<TState>:
                    throw new NotSupportedException("Wait interpretation is owned by T1-08.");
                default:
                    throw new NotSupportedException($"Node '{node.GetType().Name}' is not supported by T1-05.");
            }
        }

        EnsureInitialized(initialized, instance);
        return instance!;
    }

    private async Task<bool> ExecuteStepAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> stepNode,
        string stepPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var step = stepNode.StepFactory();
            var context = new StepContext<TState>(instance.State, resumedEvent: null, timeProvider);
            var result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

            return ApplyResult(instance, result, stepPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            Fail(instance, exception, stepPath);
            return false;
        }
    }

    private bool ApplyResult(WorkflowInstance<TState> instance, StepResult result, string stepPath)
    {
        switch (result)
        {
            case StepResult.Completed:
                return true;
            case StepResult.Failed failed:
                Fail(instance, failed.Error, stepPath);
                return false;
            case StepResult.WaitForEvent:
                throw new NotSupportedException("WaitForEvent step results are owned by T1-08.");
            case StepResult.Yield:
                throw new NotSupportedException("Yield step results are owned by T1-15.");
            default:
                throw new NotSupportedException($"Step result '{result.GetType().Name}' is not supported.");
        }
    }

    private void Fail(WorkflowInstance<TState> instance, Exception exception, string stepPath)
    {
        FireOrThrow(instance, LifecycleTrigger.Fail);
        instance.Fail(new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            timeProvider.GetUtcNow()));
    }

    private static void FireOrThrow(WorkflowInstance<TState> instance, LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(instance.Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }
    }

    private static void EnsureInitialized(bool initialized, WorkflowInstance<TState>? instance)
    {
        if (!initialized || instance is null)
        {
            throw new WorkflowDefinitionException("Workflow execution reached a node before Init created state.");
        }
    }
}
