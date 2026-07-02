using OrcaCore.Abstractions.Events;
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

        var runState = new InterpreterRunState();

        await RunSequenceAsync(
            definition.RootSequence,
            runState,
            input,
            instanceId,
            definition.DefinitionId,
            definition.DefinitionVersion,
            cancellationToken,
            startIndex: 0,
            new ResumeEventSlot(null)).ConfigureAwait(false);

        EnsureInitialized(runState.Initialized, runState.Instance);
        return runState.Instance!;
    }

    private async Task<bool> RunSequenceAsync<TInput>(
        SequenceNode<TState> sequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        CancellationToken cancellationToken,
        int startIndex,
        ResumeEventSlot resumeEvent)
    {
        for (var index = startIndex; index < sequence.Children.Count; index++)
        {
            var node = sequence.Children[index];
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case InitNode<TState> initNode:
                    var state = initNode.CreateState(input);
                    runState.Initialized = true;
                    runState.Instance = new WorkflowInstance<TState>(
                        instanceId,
                        definitionId,
                        definitionVersion,
                        state,
                        timeProvider.GetUtcNow());
                    break;
                case BusinessStepNode<TState> stepNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    var stepResult = await ExecuteStepAsync(
                        runState.Instance!,
                        stepNode,
                        node.NodeId,
                        resumeEvent,
                        cancellationToken).ConfigureAwait(false);
                    if (stepResult.Status == StepExecutionStatus.Stop)
                    {
                        return false;
                    }

                    if (stepResult.Status == StepExecutionStatus.Wait)
                    {
                        await RegisterWaitAsync(
                            runState.Instance!,
                            stepResult.EventName!,
                            stepResult.CorrelationId,
                            sequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            index + 1,
                            cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    break;
                case EndNode<TState> endNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    if (runState.Instance!.HasUnresolvedRuntimeWork)
                    {
                        Fail(
                            runState.Instance,
                            new WorkflowLifecycleException(
                                "Workflow cannot complete with unresolved runtime work."),
                            node.NodeId);
                        return false;
                    }

                    FireOrThrow(runState.Instance!, LifecycleTrigger.Complete);
                    runState.Instance!.Complete(endNode.OutcomeName, timeProvider.GetUtcNow());
                    return false;
                case IfNode<TState> ifNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    if (!TryEvaluateCondition(runState.Instance!, ifNode.Condition, node.NodeId, out var ifResult))
                    {
                        return false;
                    }

                    var selectedSequence = ifResult
                        ? ifNode.Then
                        : ifNode.Else;
                    if (!await RunSequenceAsync(
                            selectedSequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            cancellationToken,
                            startIndex: 0,
                            resumeEvent).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;
                case WhileNode<TState> whileNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!TryEvaluateCondition(runState.Instance!, whileNode.Condition, node.NodeId, out var whileResult))
                        {
                            return false;
                        }

                        if (!whileResult)
                        {
                            break;
                        }

                        if (!await RunSequenceAsync(
                                whileNode.Body,
                                runState,
                                input,
                                instanceId,
                                definitionId,
                                definitionVersion,
                                cancellationToken,
                                startIndex: 0,
                                resumeEvent).ConfigureAwait(false))
                        {
                            return false;
                        }
                    }

                    break;
                case ParallelNode<TState>:
                    throw new NotSupportedException("Parallel interpretation is owned by T1-12.");
                case WaitNode<TState> waitNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    CorrelationId correlationId;
                    try
                    {
                        correlationId = waitNode.CorrelationSelector(runState.Instance!.State);
                    }
                    catch (Exception exception)
                        when (exception is not OperationCanceledException and not NotSupportedException)
                    {
                        Fail(runState.Instance!, exception, node.NodeId);
                        return false;
                    }

                    await RegisterWaitAsync(
                        runState.Instance!,
                        waitNode.EventName,
                        correlationId,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index + 1,
                        cancellationToken).ConfigureAwait(false);
                    return false;
                default:
                    throw new NotSupportedException($"Node '{node.GetType().Name}' is not supported by T1-05.");
            }
        }

        return true;
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> stepNode,
        string stepPath,
        ResumeEventSlot resumeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var step = stepNode.StepFactory();
            var context = new StepContext<TState>(instance.State, resumeEvent.Take(), timeProvider);
            var result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

            return ApplyResult(instance, result, stepPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            Fail(instance, exception, stepPath);
            return StepExecutionResult.Stop();
        }
    }

    private StepExecutionResult ApplyResult(WorkflowInstance<TState> instance, StepResult result, string stepPath)
    {
        switch (result)
        {
            case StepResult.Completed:
                return StepExecutionResult.Continue();
            case StepResult.Failed failed:
                Fail(instance, failed.Error, stepPath);
                return StepExecutionResult.Stop();
            case StepResult.WaitForEvent wait:
                return StepExecutionResult.Wait(wait.EventName, wait.CorrelationId);
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

    private async Task RegisterWaitAsync<TInput>(
        WorkflowInstance<TState> instance,
        string eventName,
        CorrelationId correlationId,
        SequenceNode<TState> sequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int nextIndex,
        CancellationToken cancellationToken)
    {
        FireOrThrow(instance, LifecycleTrigger.EnterWait);
        var wait = instance.EnterWait(
            eventName,
            correlationId,
            timeProvider.GetUtcNow(),
            (envelope, cancellationToken) => RunSequenceAsync(
                sequence,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                cancellationToken,
                nextIndex,
                new ResumeEventSlot(envelope)));
        await instance.MatchPendingEventAsync(wait, cancellationToken).ConfigureAwait(false);
    }

    private bool TryEvaluateCondition(
        WorkflowInstance<TState> instance,
        Func<TState, bool> condition,
        string nodePath,
        out bool result)
    {
        try
        {
            result = condition(instance.State);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            Fail(instance, exception, nodePath);
            result = false;
            return false;
        }
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

    private sealed class InterpreterRunState
    {
        internal bool Initialized { get; set; }

        internal WorkflowInstance<TState>? Instance { get; set; }
    }

    private sealed class ResumeEventSlot(EventEnvelope? envelope)
    {
        private EventEnvelope? envelope = envelope;

        internal EventEnvelope? Take()
        {
            var current = envelope;
            envelope = null;
            return current;
        }
    }

    private enum StepExecutionStatus
    {
        Continue,
        Stop,
        Wait
    }

    private sealed record StepExecutionResult(
        StepExecutionStatus Status,
        string? EventName,
        CorrelationId CorrelationId)
    {
        internal static StepExecutionResult Continue()
        {
            return new StepExecutionResult(StepExecutionStatus.Continue, null, default);
        }

        internal static StepExecutionResult Stop()
        {
            return new StepExecutionResult(StepExecutionStatus.Stop, null, default);
        }

        internal static StepExecutionResult Wait(string eventName, CorrelationId correlationId)
        {
            return new StepExecutionResult(StepExecutionStatus.Wait, eventName, correlationId);
        }
    }
}
