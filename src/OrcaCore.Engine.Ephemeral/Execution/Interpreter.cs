using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
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
    private readonly WaitExecutor<TState> waitExecutor;
    private readonly WhileNodeRunner<TState> whileRunner;
    private readonly ResourceGovernanceCoordinator governance;
    private readonly YieldContinuationScheduler yieldContinuationScheduler;
    private readonly int maxConsumedEventIds;
    private readonly int maxLifecycleEvents;
    private readonly int maxPendingEvents;
    private readonly TimeSpan? stuckStepThreshold;
    private readonly int maxConcurrentExecutionPathsPerInstance;
    private readonly IServiceProvider? serviceProvider;

    internal Interpreter(
        TimeProvider timeProvider,
        StepExecutor<TState> stepExecutor,
        WorkflowFailureHandler<TState> failureHandler,
        ConditionEvaluator<TState> conditionEvaluator,
        SuspensionScheduler<TState> suspensionScheduler,
        WaitExecutor<TState> waitExecutor,
        WhileNodeRunner<TState> whileRunner,
        ResourceGovernanceCoordinator governance,
        YieldContinuationScheduler yieldContinuationScheduler,
        EphemeralWorkflowEngineOptions options,
        IServiceProvider? serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(stepExecutor);
        ArgumentNullException.ThrowIfNull(failureHandler);
        ArgumentNullException.ThrowIfNull(conditionEvaluator);
        ArgumentNullException.ThrowIfNull(suspensionScheduler);
        ArgumentNullException.ThrowIfNull(waitExecutor);
        ArgumentNullException.ThrowIfNull(whileRunner);
        ArgumentNullException.ThrowIfNull(governance);
        ArgumentNullException.ThrowIfNull(yieldContinuationScheduler);
        ArgumentNullException.ThrowIfNull(options);

        this.timeProvider = timeProvider;
        this.stepExecutor = stepExecutor;
        this.failureHandler = failureHandler;
        this.conditionEvaluator = conditionEvaluator;
        this.suspensionScheduler = suspensionScheduler;
        this.waitExecutor = waitExecutor;
        this.whileRunner = whileRunner;
        this.governance = governance;
        this.yieldContinuationScheduler = yieldContinuationScheduler;
        maxPendingEvents = options.MaxPendingEventsPerInstance;
        maxConsumedEventIds = options.MaxConsumedEventIdsPerInstance;
        maxLifecycleEvents = options.MaxLifecycleEventsPerInstance;
        stuckStepThreshold = options.StuckStepThreshold;
        maxConcurrentExecutionPathsPerInstance = options.MaxConcurrentExecutionPathsPerInstance;
        if (maxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                maxConcurrentExecutionPathsPerInstance,
                "MaxConcurrentExecutionPathsPerInstance must be positive.");
        }
        this.serviceProvider = serviceProvider;
    }

    internal async Task<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        Action<WorkflowInstance<TState>> onInitialized,
        Action<EphemeralWorkflowInstanceSnapshot> onSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(onInitialized);

        if (((CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition)).Instructions.Count > 0)
        {
            var adapter = new InMemoryExecutionStateAdapter<TState>(
                timeProvider,
                suspensionScheduler,
                governance,
                yieldContinuationScheduler,
                onSnapshotCommitted,
                stuckStepThreshold,
                maxPendingEvents,
                maxConsumedEventIds,
                maxLifecycleEvents,
                maxConcurrentExecutionPathsPerInstance,
                serviceProvider);
            return await adapter.RunAsync(
                definition,
                input,
                instanceId,
                onInitialized,
                cancellationToken).ConfigureAwait(false);
        }

        var runState = new InterpreterRunState<TState> { OnInitialized = onInitialized };
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
        deferStepFailures |= context.DeferFailures;
        if (deferStepFailures && !context.DeferFailures)
        {
            context = context with { DeferFailures = true };
        }

        for (var index = startIndex; index < context.Sequence.Children.Count; index++)
        {
            var node = context.Sequence.Children[index];
            cancellationToken.ThrowIfCancellationRequested();

            switch (node)
            {
                case InitNode<TState> initNode:
                    context.RunState.Initialized = true;
                    TState state;
                    try
                    {
                        state = initNode.CreateState(context.Input);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Workflow definition '{context.DefinitionId}' Init failed while creating state.",
                            exception);
                    }

                    context.RunState.Instance = new WorkflowInstance<TState>(
                        context.InstanceId,
                        context.DefinitionId,
                        context.DefinitionVersion,
                        state,
                        timeProvider.GetUtcNow(),
                        maxPendingEvents,
                        maxConsumedEventIds,
                        maxLifecycleEvents);
                    context.RunState.OnInitialized(context.RunState.Instance);
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
                    await whileRunner.RunAsync(whileNode, context, index, this, cancellationToken).ConfigureAwait(false);
                    return false;

                case WaitNode<TState> waitNode:
                    await waitExecutor
                        .ExecuteAsync(
                            waitNode,
                            node.NodeId,
                            EnsureInitialized(context.RunState),
                            context,
                            index,
                            this,
                            cancellationToken,
                            deferStepFailures)
                        .ConfigureAwait(false);
                    return false;

                case DelayNode<TState> delayNode:
                    suspensionScheduler.RegisterDelay(
                        EnsureInitialized(context.RunState),
                        delayNode.Duration,
                        context,
                        nextIndex: index + 1,
                        this);
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
            context.ForEachItem,
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
                    stepResult.EventContract!,
                    stepResult.CorrelationId!,
                    timeout: null,
                    nodeId,
                    context,
                    nextIndex: stepIndex + 1,
                    this,
                    cancellationToken).ConfigureAwait(false);
                return false;
            case StepExecutionStatus.Yield:
                yieldContinuationScheduler.Schedule(
                    instance,
                    this,
                    context,
                    stepIndex);
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
        instance.Complete(endNode.ResolveOutcome(instance.State), timeProvider.GetUtcNow());
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
        if (!conditionEvaluator.TryEvaluate(
                instance,
                context.RunState,
                ifNode.Condition,
                nodeId,
                context.DeferFailures,
                out var ifResult))
        {
            return false;
        }

        var childContext = context.CreateNested(
            ifResult ? ifNode.Then : ifNode.Else,
            context.BranchId,
            context.ResumeEvent,
            continuationToken => ContinueSequenceAsync(context, ifIndex + 1, continuationToken));

        return await RunSequenceAsync(
            childContext,
            startIndex: 0,
            cancellationToken,
            context.DeferFailures).ConfigureAwait(false);
    }

    private static WorkflowInstance<TState> EnsureInitialized(InterpreterRunState<TState> runState)
    {
        if (!runState.Initialized || runState.Instance is null)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Workflow execution reached a node before Init created state.");
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
