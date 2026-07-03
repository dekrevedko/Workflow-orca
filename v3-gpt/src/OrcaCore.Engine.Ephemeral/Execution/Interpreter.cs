using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Governance;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class Interpreter<TState> : ISequenceExecutionEngine<TState>
{
    private readonly TimeProvider timeProvider;
    private readonly StepExecutor<TState> stepExecutor;
    private readonly WorkflowFailureHandler<TState> failureHandler;
    private readonly ConditionEvaluator<TState> conditionEvaluator;
    private readonly SuspensionScheduler<TState> suspensionScheduler;
    private readonly WhileNodeRunner<TState> whileRunner;
    private readonly ParallelNodeRunner<TState> parallelRunner;
    private readonly WhenFirstNodeRunner<TState> whenFirstRunner;
    private readonly ForEachNodeRunner<TState> forEachRunner;

    internal Interpreter(
        TimeProvider timeProvider,
        EphemeralTimerService timerService,
        ResourceGovernanceCoordinator governance,
        TimeSpan? stuckStepThreshold = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(timerService);
        ArgumentNullException.ThrowIfNull(governance);

        this.timeProvider = timeProvider;
        stepExecutor = new StepExecutor<TState>(timeProvider, governance, stuckStepThreshold);
        failureHandler = new WorkflowFailureHandler<TState>(timeProvider);
        conditionEvaluator = new ConditionEvaluator<TState>(failureHandler);
        suspensionScheduler = new SuspensionScheduler<TState>(timeProvider, timerService, this);
        whileRunner = new WhileNodeRunner<TState>(this, conditionEvaluator);
        parallelRunner = new ParallelNodeRunner<TState>(this);
        whenFirstRunner = new WhenFirstNodeRunner<TState>(this, timeProvider);
        forEachRunner = new ForEachNodeRunner<TState>(this, timeProvider);
    }

    internal async Task<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var runState = new InterpreterRunState<TState>();
        var context = new SequenceExecutionContext<TState, TInput>(
            definition.RootSequence,
            runState,
            input,
            instanceId,
            definition.DefinitionId,
            definition.DefinitionVersion,
            branchId: null,
            new ResumeEventSlot(null),
            afterSequence: null);

        await RunSequenceAsync(context, startIndex: 0, cancellationToken).ConfigureAwait(false);

        EnsureInitialized(runState);
        return runState.Instance!;
    }

    private async Task<bool> RunSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken,
        bool deferStepFailures = false)
    {
        for (var index = startIndex; index < context.Sequence.Children.Count; index++)
        {
            var node = context.Sequence.Children[index];
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case InitNode<TState> initNode:
                    context.RunState.Initialized = true;
                    context.RunState.Instance = new WorkflowInstance<TState>(
                        context.InstanceId,
                        context.DefinitionId,
                        context.DefinitionVersion,
                        initNode.CreateState(context.Input),
                        timeProvider.GetUtcNow());
                    break;

                case BusinessStepNode<TState> stepNode:
                    if (!await RunStepAsync(stepNode, node.NodeId, context, index, cancellationToken, deferStepFailures)
                            .ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;

                case EndNode<TState> endNode:
                    return Complete(endNode, node.NodeId, context.RunState);

                case IfNode<TState> ifNode:
                    if (!await RunIfAsync(ifNode, node.NodeId, context, index, cancellationToken).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;

                case WhileNode<TState> whileNode:
                    EnsureInitialized(context.RunState);
                    await whileRunner.RunAsync(whileNode, context, index, cancellationToken).ConfigureAwait(false);
                    return false;

                case ParallelNode<TState> parallelNode:
                    EnsureInitialized(context.RunState);
                    await parallelRunner.RunAsync(parallelNode, context, index, cancellationToken).ConfigureAwait(false);
                    return false;

                case WhenFirstNode<TState> whenFirstNode:
                    EnsureInitialized(context.RunState);
                    await whenFirstRunner.RunAsync(whenFirstNode, context, index, cancellationToken).ConfigureAwait(false);
                    return false;

                case ForEachNode<TState> forEachNode:
                    EnsureInitialized(context.RunState);
                    await forEachRunner.RunAsync(forEachNode, context, index, cancellationToken).ConfigureAwait(false);
                    return false;

                case RunChildNode<TState>:
                case RunChildrenNode<TState>:
                    failureHandler.Fail(
                        EnsureInitialized(context.RunState),
                        new NotSupportedException("Durable child workflow nodes require the durable engine."),
                        node.NodeId);
                    return false;

                case WaitNode<TState> waitNode:
                    await RunWaitAsync(waitNode, node.NodeId, context, index, cancellationToken).ConfigureAwait(false);
                    return false;

                case DelayNode<TState> delayNode:
                    suspensionScheduler.RegisterDelay(
                        EnsureInitialized(context.RunState),
                        delayNode.Duration,
                        context,
                        nextIndex: index + 1);
                    return false;

                default:
                    throw new NotSupportedException($"Node '{node.GetType().Name}' is not supported by T1-05.");
            }
        }

        return true;
    }

    private async Task ContinueSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken)
    {
        var completed = await RunSequenceAsync(context, startIndex, cancellationToken).ConfigureAwait(false);
        if (completed && context.AfterSequence is not null)
        {
            await context.AfterSequence(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> RunStepAsync<TInput>(
        BusinessStepNode<TState> stepNode,
        string nodeId,
        SequenceExecutionContext<TState, TInput> context,
        int stepIndex,
        CancellationToken cancellationToken,
        bool deferStepFailures)
    {
        var instance = EnsureInitialized(context.RunState);
        var stepResult = await stepExecutor.ExecuteAsync(
            instance,
            stepNode,
            nodeId,
            context.ResumeEvent.Take(),
            cancellationToken,
            deferStepFailures).ConfigureAwait(false);

        switch (stepResult.Status)
        {
            case StepExecutionStatus.Continue:
                return true;
            case StepExecutionStatus.Failed:
                context.RunState.DeferredFailure = stepResult.Error;
                return false;
            case StepExecutionStatus.Stop:
                return false;
            case StepExecutionStatus.Wait:
                await suspensionScheduler.RegisterWaitAsync(
                    instance,
                    stepResult.EventName!,
                    stepResult.CorrelationId,
                    timeout: null,
                    context,
                    nextIndex: stepIndex + 1,
                    cancellationToken).ConfigureAwait(false);
                return false;
            case StepExecutionStatus.Yield:
                instance.ScheduleYield(continuationToken => ContinueSequenceAsync(
                    context,
                    stepIndex,
                    continuationToken));
                return false;
            default:
                throw new NotSupportedException(
                    $"Step execution status '{stepResult.Status}' is not supported.");
        }
    }

    private bool Complete(
        EndNode<TState> endNode,
        string nodeId,
        InterpreterRunState<TState> runState)
    {
        var instance = EnsureInitialized(runState);
        if (instance.HasUnresolvedRuntimeWork)
        {
            failureHandler.Fail(
                instance,
                new WorkflowLifecycleException("Workflow cannot complete with unresolved runtime work."),
                nodeId);
            return false;
        }

        WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.Complete);
        instance.Complete(endNode.OutcomeName, timeProvider.GetUtcNow());
        return false;
    }

    private async Task<bool> RunIfAsync<TInput>(
        IfNode<TState> ifNode,
        string nodeId,
        SequenceExecutionContext<TState, TInput> context,
        int ifIndex,
        CancellationToken cancellationToken)
    {
        var instance = EnsureInitialized(context.RunState);
        if (!conditionEvaluator.TryEvaluate(instance, ifNode.Condition, nodeId, out var ifResult))
        {
            return false;
        }

        var childContext = context.CreateNested(
            ifResult ? ifNode.Then : ifNode.Else,
            context.BranchId,
            context.ResumeEvent,
            continuationToken => ContinueSequenceAsync(context, ifIndex + 1, continuationToken));

        return await RunSequenceAsync(childContext, startIndex: 0, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunWaitAsync<TInput>(
        WaitNode<TState> waitNode,
        string nodeId,
        SequenceExecutionContext<TState, TInput> context,
        int waitIndex,
        CancellationToken cancellationToken)
    {
        var instance = EnsureInitialized(context.RunState);
        CorrelationId correlationId;
        try
        {
            correlationId = waitNode.CorrelationSelector(instance.State);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and not NotSupportedException)
        {
            failureHandler.Fail(instance, exception, nodeId);
            return;
        }

        await suspensionScheduler.RegisterWaitAsync(
            instance,
            waitNode.EventName,
            correlationId,
            waitNode.Timeout,
            context,
            nextIndex: waitIndex + 1,
            cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowInstance<TState> EnsureInitialized(InterpreterRunState<TState> runState)
    {
        if (!runState.Initialized || runState.Instance is null)
        {
            throw new WorkflowDefinitionException("Workflow execution reached a node before Init created state.");
        }

        return runState.Instance;
    }

    Task<bool> ISequenceExecutionEngine<TState>.RunSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken,
        bool deferStepFailures)
    {
        return RunSequenceAsync(context, startIndex, cancellationToken, deferStepFailures);
    }

    Task ISequenceExecutionEngine<TState>.ContinueSequenceAsync<TInput>(
        SequenceExecutionContext<TState, TInput> context,
        int startIndex,
        CancellationToken cancellationToken)
    {
        return ContinueSequenceAsync(context, startIndex, cancellationToken);
    }

    void ISequenceExecutionEngine<TState>.Fail(
        WorkflowInstance<TState> instance,
        Exception exception,
        string stepPath)
    {
        failureHandler.Fail(instance, exception, stepPath);
    }
}
