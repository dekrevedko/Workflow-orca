using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Governance;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class Interpreter<TState>
{
    private readonly TimeSpan? stuckStepThreshold;
    private readonly ResourceGovernanceCoordinator governance;
    private readonly EphemeralTimerService timerService;
    private readonly TimeProvider timeProvider;

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
        this.timerService = timerService;
        this.governance = governance;
        this.stuckStepThreshold = stuckStepThreshold;
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
            branchId: null,
            new ResumeEventSlot(null),
            afterSequence: null).ConfigureAwait(false);

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
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence,
        bool deferStepFailures = false)
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
                        cancellationToken,
                        deferStepFailures).ConfigureAwait(false);
                    if (stepResult.Status == StepExecutionStatus.Failed)
                    {
                        runState.DeferredFailure = stepResult.Error;
                        return false;
                    }

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
                            null,
                            branchId,
                            sequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            index + 1,
                            cancellationToken,
                            afterSequence).ConfigureAwait(false);
                        return false;
                    }

                    if (stepResult.Status == StepExecutionStatus.Yield)
                    {
                        runState.Instance!.ScheduleYield(continuationToken => ContinueSequenceAsync(
                            sequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            index,
                            branchId,
                            resumeEvent,
                            afterSequence,
                            continuationToken));
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
                            branchId,
                            resumeEvent,
                            continuationToken => ContinueSequenceAsync(
                                sequence,
                                runState,
                                input,
                                instanceId,
                                definitionId,
                                definitionVersion,
                                index + 1,
                                branchId,
                                resumeEvent,
                                afterSequence,
                                continuationToken)).ConfigureAwait(false))
                    {
                        return false;
                    }

                    break;
                case WhileNode<TState> whileNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    await ContinueWhileAsync(
                        whileNode,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index,
                        branchId,
                        resumeEvent,
                        afterSequence,
                        cancellationToken).ConfigureAwait(false);
                    return false;
                case ParallelNode<TState> parallelNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    var join = new ParallelJoin(
                        parallelNode.Branches.Count,
                        continuationToken => ContinueSequenceAsync(
                            sequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            index + 1,
                            branchId,
                            resumeEvent,
                            afterSequence,
                            continuationToken));
                    foreach (var branch in parallelNode.Branches)
                    {
                        var completed = await RunSequenceAsync(
                            branch.Sequence,
                            runState,
                            input,
                            instanceId,
                            definitionId,
                            definitionVersion,
                            cancellationToken,
                            startIndex: 0,
                            branch.BranchId,
                            resumeEvent,
                            continuationToken => join.BranchCompletedAsync(continuationToken))
                            .ConfigureAwait(false);
                        if (completed)
                        {
                            await join.BranchCompletedAsync(cancellationToken).ConfigureAwait(false);
                        }

                        if (runState.Instance!.Status == WorkflowStatus.Failed)
                        {
                            return false;
                        }
                    }

                    return false;
                case WhenFirstNode<TState> whenFirstNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    await RunWhenFirstAsync(
                        whenFirstNode,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index,
                        branchId,
                        resumeEvent,
                        afterSequence,
                        cancellationToken).ConfigureAwait(false);
                    return false;
                case ForEachNode<TState> forEachNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    await RunForEachAsync(
                        forEachNode,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index,
                        branchId,
                        resumeEvent,
                        afterSequence,
                        cancellationToken).ConfigureAwait(false);
                    return false;
                case RunChildNode<TState>:
                case RunChildrenNode<TState>:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    Fail(
                        runState.Instance!,
                        new NotSupportedException("Durable child workflow nodes require the durable engine."),
                        node.NodeId);
                    return false;
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
                        waitNode.Timeout,
                        branchId,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index + 1,
                        cancellationToken,
                        afterSequence).ConfigureAwait(false);
                    return false;
                case DelayNode<TState> delayNode:
                    EnsureInitialized(runState.Initialized, runState.Instance);
                    RegisterDelay(
                        runState.Instance!,
                        delayNode.Duration,
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        index + 1,
                        branchId,
                        resumeEvent,
                        afterSequence);
                    return false;
                default:
                    throw new NotSupportedException($"Node '{node.GetType().Name}' is not supported by T1-05.");
            }
        }

        return true;
    }

    private async Task ContinueSequenceAsync<TInput>(
        SequenceNode<TState> sequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int startIndex,
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence,
        CancellationToken cancellationToken)
    {
        var completed = await RunSequenceAsync(
            sequence,
            runState,
            input,
            instanceId,
            definitionId,
            definitionVersion,
            cancellationToken,
            startIndex,
            branchId,
            resumeEvent,
            afterSequence).ConfigureAwait(false);
        if (completed && afterSequence is not null)
        {
            await afterSequence(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunWhenFirstAsync<TInput>(
        WhenFirstNode<TState> whenFirstNode,
        SequenceNode<TState> parentSequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int whenFirstIndex,
        BranchId? parentBranchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence,
        CancellationToken cancellationToken)
    {
        var join = new WhenFirstJoin(
            whenFirstNode,
            runState.Instance!,
            () => timeProvider.GetUtcNow(),
            continuationToken => ContinueSequenceAsync(
                parentSequence,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                whenFirstIndex + 1,
                parentBranchId,
                resumeEvent,
                afterSequence,
                continuationToken));
        foreach (var branch in whenFirstNode.Branches)
        {
            if (join.ShouldStopScheduling)
            {
                return;
            }

            var completed = await RunSequenceAsync(
                branch.Sequence,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                cancellationToken,
                startIndex: 0,
                branch.BranchId,
                resumeEvent,
                continuationToken => join.BranchCompletedAsync(branch, continuationToken))
                .ConfigureAwait(false);
            if (completed)
            {
                await join.BranchCompletedAsync(branch, cancellationToken).ConfigureAwait(false);
            }

            if (runState.Instance!.Status == WorkflowStatus.Failed)
            {
                return;
            }
        }
    }

    private async Task RunForEachAsync<TInput>(
        ForEachNode<TState> forEachNode,
        SequenceNode<TState> parentSequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int forEachIndex,
        BranchId? parentBranchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ForEachWorkItemSnapshot> workItems;
        try
        {
            workItems = forEachNode.MaterializeWorkItems(runState.Instance!.State);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            Fail(runState.Instance!, exception, forEachNode.NodeId);
            return;
        }

        var group = runState.Instance!.RecordForEachGroup(
            forEachNode.NodeId,
            workItems,
            forEachNode.MaxConcurrency,
            timeProvider.GetUtcNow());
        if (workItems.Count == 0)
        {
            await ContinueSequenceAsync(
                parentSequence,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                forEachIndex + 1,
                parentBranchId,
                resumeEvent,
                afterSequence,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var gate = new object();
        var finishedItems = new HashSet<int>();
        var activeCount = 0;
        var nextOrdinal = 0;
        var continued = 0;
        Exception? firstFailure = null;
        var maxConcurrency = forEachNode.MaxConcurrency ?? workItems.Count;

        async Task DispatchAvailableAsync(CancellationToken dispatchToken)
        {
            while (true)
            {
                int workItemIndex;
                lock (gate)
                {
                    if (activeCount >= maxConcurrency ||
                        nextOrdinal >= workItems.Count ||
                        runState.Instance!.Status == WorkflowStatus.Failed ||
                        (forEachNode.JoinPolicy is ForEachJoinPolicy.WhenAny &&
                            Interlocked.CompareExchange(ref continued, 0, 0) == 1))
                    {
                        return;
                    }

                    workItemIndex = workItems[nextOrdinal].Index;
                    nextOrdinal++;
                    activeCount++;
                    runState.Instance.StartForEachWorkItem(group, workItemIndex, timeProvider.GetUtcNow());
                }

                var completed = await RunSequenceAsync(
                    forEachNode.Body,
                    runState,
                    input,
                    instanceId,
                    definitionId,
                    definitionVersion,
                    dispatchToken,
                    startIndex: 0,
                    new BranchId(workItemIndex, $"item-{workItemIndex}"),
                    resumeEvent,
                    itemToken => ItemCompletedAsync(workItemIndex, itemToken),
                    deferStepFailures: true).ConfigureAwait(false);

                var itemFailure = runState.TakeDeferredFailure();
                if (itemFailure is not null)
                {
                    await ItemFailedAsync(workItemIndex, itemFailure, dispatchToken).ConfigureAwait(false);
                    if (runState.Instance!.Status == WorkflowStatus.Failed)
                    {
                        return;
                    }

                    continue;
                }

                if (runState.Instance!.Status == WorkflowStatus.Failed)
                {
                    return;
                }

                if (completed)
                {
                    await ItemCompletedAsync(workItemIndex, dispatchToken).ConfigureAwait(false);
                }
            }
        }

        async Task ItemCompletedAsync(int workItemIndex, CancellationToken itemToken)
        {
            var shouldContinueParent = false;
            var shouldFailParent = false;
            lock (gate)
            {
                if (!finishedItems.Add(workItemIndex))
                {
                    return;
                }

                activeCount--;
                runState.Instance!.CompleteForEachWorkItem(group, workItemIndex, timeProvider.GetUtcNow());
                if (forEachNode.JoinPolicy is ForEachJoinPolicy.WhenAny)
                {
                    if (forEachNode.ResidualPolicy is ForEachResidualPolicy.CancelRemaining)
                    {
                        var cancelled = runState.Instance.CancelForEachResidualWork(
                            group,
                            workItemIndex,
                            timeProvider.GetUtcNow());
                        foreach (var cancelledIndex in cancelled)
                        {
                            runState.Instance.ResolveBranchRuntimeWork(
                                new BranchId(cancelledIndex, $"item-{cancelledIndex}"));
                        }

                        shouldContinueParent = true;
                    }
                    else
                    {
                        shouldContinueParent = finishedItems.Count == workItems.Count;
                    }
                }
                else if (finishedItems.Count == workItems.Count)
                {
                    shouldFailParent = firstFailure is not null &&
                        forEachNode.FailurePolicy is ForEachFailurePolicy.WaitAllThenFail;
                    shouldContinueParent = !shouldFailParent;
                }
            }

            if (shouldFailParent)
            {
                Fail(runState.Instance!, firstFailure!, forEachNode.NodeId);
                return;
            }

            if (shouldContinueParent)
            {
                if (Interlocked.Exchange(ref continued, 1) == 0)
                {
                    await ContinueSequenceAsync(
                        parentSequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        forEachIndex + 1,
                        parentBranchId,
                        resumeEvent,
                        afterSequence,
                        itemToken).ConfigureAwait(false);
                }

                return;
            }

            await DispatchAvailableAsync(itemToken).ConfigureAwait(false);
        }

        async Task ItemFailedAsync(int workItemIndex, Exception exception, CancellationToken itemToken)
        {
            var shouldContinueParent = false;
            var shouldFailParent = false;
            lock (gate)
            {
                if (!finishedItems.Add(workItemIndex))
                {
                    return;
                }

                firstFailure ??= exception;
                activeCount--;
                runState.Instance!.FailForEachWorkItem(
                    group,
                    workItemIndex,
                    exception.Message,
                    timeProvider.GetUtcNow());
                shouldFailParent = forEachNode.FailurePolicy is ForEachFailurePolicy.FailFast ||
                    (forEachNode.FailurePolicy is ForEachFailurePolicy.WaitAllThenFail &&
                        finishedItems.Count == workItems.Count);
                shouldContinueParent = forEachNode.FailurePolicy is ForEachFailurePolicy.ContinueWithPartialFailures &&
                    finishedItems.Count == workItems.Count;
            }

            if (shouldFailParent)
            {
                Fail(runState.Instance!, firstFailure!, forEachNode.NodeId);
                return;
            }

            if (shouldContinueParent)
            {
                if (Interlocked.Exchange(ref continued, 1) == 0)
                {
                    await ContinueSequenceAsync(
                        parentSequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        forEachIndex + 1,
                        parentBranchId,
                        resumeEvent,
                        afterSequence,
                        itemToken).ConfigureAwait(false);
                }

                return;
            }

            await DispatchAvailableAsync(itemToken).ConfigureAwait(false);
        }

        await DispatchAvailableAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ContinueWhileAsync<TInput>(
        WhileNode<TState> whileNode,
        SequenceNode<TState> parentSequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int whileIndex,
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryEvaluateCondition(runState.Instance!, whileNode.Condition, whileNode.NodeId, out var whileResult))
            {
                return;
            }

            if (!whileResult)
            {
                await ContinueSequenceAsync(
                    parentSequence,
                    runState,
                    input,
                    instanceId,
                    definitionId,
                    definitionVersion,
                    whileIndex + 1,
                    branchId,
                    resumeEvent,
                    afterSequence,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var completedBody = await RunSequenceAsync(
                whileNode.Body,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                cancellationToken,
                startIndex: 0,
                branchId,
                resumeEvent,
                continuationToken => ContinueWhileAsync(
                    whileNode,
                    parentSequence,
                    runState,
                    input,
                    instanceId,
                    definitionId,
                    definitionVersion,
                    whileIndex,
                    branchId,
                    resumeEvent,
                    afterSequence,
                    continuationToken)).ConfigureAwait(false);
            if (!completedBody)
            {
                return;
            }
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> stepNode,
        string stepPath,
        ResumeEventSlot resumeEvent,
        CancellationToken cancellationToken,
        bool deferFailures)
    {
        var timedOut = false;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutTimer = stepNode.Policies.Timeout is { } timeout
            ? timeProvider.CreateTimer(
                _ =>
                {
                    timedOut = true;
                    timeoutCancellation.Cancel();
                },
                null,
                timeout.Duration,
                Timeout.InfiniteTimeSpan)
            : null;
        var executionToken = timeoutTimer is null
            ? cancellationToken
            : timeoutCancellation.Token;
        await using var governanceLease = await governance
            .EnterStepAsync(stepNode.Policies.PoolKey, executionToken)
            .ConfigureAwait(false);
        var maxAttempts = stepNode.Policies.Retry?.MaxAttempts ?? 1;
        var step = stepNode.StepFactory();
        var resumedEvent = resumeEvent.Take();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stepStartedAt = timeProvider.GetUtcNow();
            instance.StartStep(stepPath, stepStartedAt, stepNode.Policies.Timeout?.Duration);
            try
            {
                var context = new StepContext<TState>(instance.State, resumedEvent, timeProvider);
                var result = await step.ExecuteAsync(context, executionToken).ConfigureAwait(false);
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (result is StepResult.Failed && attempt < maxAttempts)
                {
                    continue;
                }

                return ApplyResult(instance, result, stepPath, deferFailures);
            }
            catch (OperationCanceledException) when (timedOut && !cancellationToken.IsCancellationRequested)
            {
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                var timeoutException = new TimeoutException(
                    $"Step '{stepPath}' timed out after {stepNode.Policies.Timeout!.Duration}.");
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(timeoutException);
                }

                Fail(instance, timeoutException, stepPath);
                return StepExecutionResult.Stop();
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
            {
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (attempt < maxAttempts)
                {
                    continue;
                }

                if (deferFailures)
                {
                    return StepExecutionResult.Failed(exception);
                }

                Fail(instance, exception, stepPath);
                return StepExecutionResult.Stop();
            }
        }

        return StepExecutionResult.Stop();
    }

    private void RecordStuckStepIfNeeded(
        WorkflowInstance<TState> instance,
        string stepPath,
        DateTimeOffset stepStartedAt)
    {
        if (stuckStepThreshold is not { } threshold)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (now - stepStartedAt > threshold)
        {
            instance.MarkStuckStep(stepPath, now);
        }
    }

    private StepExecutionResult ApplyResult(
        WorkflowInstance<TState> instance,
        StepResult result,
        string stepPath,
        bool deferFailures)
    {
        switch (result)
        {
            case StepResult.Completed:
                instance.RecordLifecycleEvent("StepCompleted", stepPath, WorkflowStatus.Running, timeProvider.GetUtcNow());
                return StepExecutionResult.Continue();
            case StepResult.Failed failed:
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(failed.Error);
                }

                Fail(instance, failed.Error, stepPath);
                return StepExecutionResult.Stop();
            case StepResult.WaitForEvent wait:
                return StepExecutionResult.Wait(wait.EventName, wait.CorrelationId);
            case StepResult.Yield:
                return StepExecutionResult.Yield();
            default:
                throw new NotSupportedException($"Step result '{result.GetType().Name}' is not supported.");
        }
    }

    private void Fail(WorkflowInstance<TState> instance, Exception exception, string stepPath)
    {
        var occurredAt = timeProvider.GetUtcNow();
        instance.RecordLifecycleEvent("StepFailed", stepPath, WorkflowStatus.Failed, occurredAt);
        FireOrThrow(instance, LifecycleTrigger.Fail);
        instance.Fail(new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt));
    }

    private async Task RegisterWaitAsync<TInput>(
        WorkflowInstance<TState> instance,
        string eventName,
        CorrelationId correlationId,
        TimeSpan? timeout,
        BranchId? branchId,
        SequenceNode<TState> sequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int nextIndex,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? afterSequence)
    {
        if (instance.Status == WorkflowStatus.Running)
        {
            FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var wait = instance.EnterWait(
            eventName,
            correlationId,
            branchId,
            timeProvider.GetUtcNow(),
            (envelope, cancellationToken) => ContinueSequenceAsync(
                sequence,
                runState,
                input,
                instanceId,
                definitionId,
                definitionVersion,
                nextIndex,
                branchId,
                new ResumeEventSlot(envelope),
                afterSequence,
                cancellationToken));
        if (timeout is { } timeoutDuration)
        {
            var timeoutTimer = timerService.Schedule(
                instanceId,
                timeoutDuration,
                timerCancellationToken => instance.FireWaitTimeoutAsync(
                    wait,
                    timeProvider.GetUtcNow(),
                    continuationToken => ContinueSequenceAsync(
                        sequence,
                        runState,
                        input,
                        instanceId,
                        definitionId,
                        definitionVersion,
                        nextIndex,
                        branchId,
                        new ResumeEventSlot(null),
                        afterSequence,
                        continuationToken),
                    timerCancellationToken));
            wait.SetCancelLoser(() => timerService.Cancel(timeoutTimer));
        }

        await instance.MatchPendingEventAsync(wait, cancellationToken).ConfigureAwait(false);
    }

    private void RegisterDelay<TInput>(
        WorkflowInstance<TState> instance,
        TimeSpan duration,
        SequenceNode<TState> sequence,
        InterpreterRunState runState,
        TInput input,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int nextIndex,
        BranchId? branchId,
        ResumeEventSlot resumeEvent,
        Func<CancellationToken, Task>? afterSequence)
    {
        if (instance.Status == WorkflowStatus.Running)
        {
            FireOrThrow(instance, LifecycleTrigger.EnterWait);
        }

        var registeredAt = timeProvider.GetUtcNow();
        var timer = instance.EnterDelay(branchId, registeredAt);
        var scheduledTimer = timerService.Schedule(
            instanceId,
            duration,
            cancellationToken => instance.FireDelayAsync(
                timer,
                timeProvider.GetUtcNow(),
                continuationToken => ContinueSequenceAsync(
                    sequence,
                    runState,
                    input,
                    instanceId,
                    definitionId,
                    definitionVersion,
                    nextIndex,
                    branchId,
                    resumeEvent,
                    afterSequence,
                    continuationToken),
                cancellationToken));
        timer.SetCancel(() => timerService.Cancel(scheduledTimer));
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

        internal Exception? DeferredFailure { get; set; }

        internal Exception? TakeDeferredFailure()
        {
            var failure = DeferredFailure;
            DeferredFailure = null;
            return failure;
        }
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

    private sealed class ParallelJoin(
        int branchCount,
        Func<CancellationToken, Task> continueAsync)
    {
        private int remaining = branchCount;
        private int continued;

        internal async Task BranchCompletedAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Decrement(ref remaining) == 0 &&
                Interlocked.Exchange(ref continued, 1) == 0)
            {
                await continueAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed class WhenFirstJoin(
        WhenFirstNode<TState> node,
        WorkflowInstance<TState> instance,
        Func<DateTimeOffset> getUtcNow,
        Func<CancellationToken, Task> continueAsync)
    {
        private readonly object gate = new();
        private readonly HashSet<BranchId> completedBranches = [];
        private int continued;
        private BranchId? winner;

        internal bool ShouldStopScheduling
        {
            get
            {
                lock (gate)
                {
                    return node.ResidualPolicy is not WhenFirstResidualPolicy.LetRemainingComplete &&
                        winner is not null;
                }
            }
        }

        internal async Task BranchCompletedAsync(ParallelBranch<TState> branch, CancellationToken cancellationToken)
        {
            var shouldContinue = false;
            lock (gate)
            {
                if (!completedBranches.Add(branch.BranchId))
                {
                    return;
                }

                var recordedAt = getUtcNow();
                if (winner is null)
                {
                    winner = branch.BranchId;
                    instance.RecordCompositionBranchOutcome(node.NodeId, branch.BranchId, "Winner", recordedAt);
                    switch (node.ResidualPolicy)
                    {
                        case WhenFirstResidualPolicy.CancelRemaining:
                            RecordResidualBranches("Cancelled", recordedAt);
                            shouldContinue = true;
                            break;
                        case WhenFirstResidualPolicy.IgnoreRemaining:
                            RecordResidualBranches("Ignored", recordedAt);
                            shouldContinue = true;
                            break;
                        case WhenFirstResidualPolicy.LetRemainingComplete:
                            shouldContinue = completedBranches.Count == node.Branches.Count;
                            break;
                        default:
                            throw new NotSupportedException(
                                $"WhenFirst residual policy '{node.ResidualPolicy}' is not supported.");
                    }
                }
                else
                {
                    instance.RecordCompositionBranchOutcome(node.NodeId, branch.BranchId, "Completed", recordedAt);
                    shouldContinue = node.ResidualPolicy is WhenFirstResidualPolicy.LetRemainingComplete &&
                        completedBranches.Count == node.Branches.Count;
                }
            }

            if (shouldContinue && Interlocked.Exchange(ref continued, 1) == 0)
            {
                await continueAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private void RecordResidualBranches(string status, DateTimeOffset recordedAt)
        {
            foreach (var residualBranch in node.Branches.Where(candidate => candidate.BranchId != winner))
            {
                instance.RecordCompositionBranchOutcome(node.NodeId, residualBranch.BranchId, status, recordedAt);
                instance.ResolveBranchRuntimeWork(residualBranch.BranchId);
            }
        }
    }

    private enum StepExecutionStatus
    {
        Continue,
        Stop,
        Wait,
        Yield,
        Failed
    }

    private sealed record StepExecutionResult(
        StepExecutionStatus Status,
        string? EventName,
        CorrelationId CorrelationId,
        Exception? Error)
    {
        internal static StepExecutionResult Continue()
        {
            return new StepExecutionResult(StepExecutionStatus.Continue, null, default, null);
        }

        internal static StepExecutionResult Stop()
        {
            return new StepExecutionResult(StepExecutionStatus.Stop, null, default, null);
        }

        internal static StepExecutionResult Wait(string eventName, CorrelationId correlationId)
        {
            return new StepExecutionResult(StepExecutionStatus.Wait, eventName, correlationId, null);
        }

        internal static StepExecutionResult Yield()
        {
            return new StepExecutionResult(StepExecutionStatus.Yield, null, default, null);
        }

        internal static StepExecutionResult Failed(Exception exception)
        {
            return new StepExecutionResult(StepExecutionStatus.Failed, null, default, exception);
        }
    }
}
