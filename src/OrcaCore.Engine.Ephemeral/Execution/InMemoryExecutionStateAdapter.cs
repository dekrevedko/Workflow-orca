using System.Collections;
using System.Reflection;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Governance;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>(
    TimeProvider timeProvider,
    SuspensionScheduler<TState> suspensionScheduler,
    ResourceGovernanceCoordinator governance,
    YieldContinuationScheduler yieldContinuationScheduler,
    Action<EphemeralWorkflowInstanceSnapshot> onSnapshotCommitted,
    TimeSpan? stuckStepThreshold,
    int maxPendingEvents,
    int maxConsumedEventIds,
    int maxLifecycleEvents,
    int maxConcurrentExecutionPathsPerInstance,
    IServiceProvider? serviceProvider)
{
    private StructuredEphemeralValueCodec codec = null!;
    private readonly Dictionary<FiberId, EventEnvelope> resumedEvents = [];
    private readonly Dictionary<FiberId, GovernanceLease> grantedStepLeases = [];
    private readonly Dictionary<FiberId, RuntimeWaitRecord> waitsByFiber = [];
    private readonly Dictionary<FiberId, RuntimeTimerRecord> timersByFiber = [];
    private WorkflowDefinition<TState>? activeDefinition;
    private WorkflowInstance<TState>? activeInstance;
    private CompiledWorkflowPlan? activePlan;
    private StructuredExecutionState? activeExecution;

    internal async Task<WorkflowInstance<TState>> RunAsync<TInput>(
        WorkflowDefinition<TState> definition,
        TInput input,
        InstanceId instanceId,
        Action<WorkflowInstance<TState>> onInitialized,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(onInitialized);

        var init = definition.RootSequence.Children.OfType<InitNode<TState>>().Single();
        TState initialState;
        try
        {
            initialState = init.CreateState(input);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                $"Workflow definition '{definition.DefinitionId}' Init failed while creating state.",
                exception);
        }

        var instance = new WorkflowInstance<TState>(
            instanceId,
            definition.DefinitionId,
            definition.DefinitionVersion,
            initialState,
            timeProvider.GetUtcNow(),
            maxPendingEvents,
            maxConsumedEventIds,
            maxLifecycleEvents);
        onInitialized(instance);

        activeDefinition = definition;
        activeInstance = instance;
        activePlan = (CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition);
        codec = new StructuredEphemeralValueCodec();
        activeExecution = StructuredExecutionState.Create(
            instanceId,
            generation: 0,
            activePlan.Instructions[0].Id);
        if (activePlan.WorkflowTimeout is { } workflowTimeout)
        {
            suspensionScheduler.RegisterWorkflowDeadline(
                instance,
                instance.CreatedAt.Add(workflowTimeout));
        }

        await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
        return instance;
    }

    private async Task RunUntilBoundaryAsync(CancellationToken cancellationToken)
    {
        var definition = activeDefinition ??
            throw new InvalidOperationException("The structured execution session is not initialized.");
        var instance = activeInstance ??
            throw new InvalidOperationException("The structured execution instance is not initialized.");
        var plan = activePlan ??
            throw new InvalidOperationException("The structured execution plan is not initialized.");
        var quantumBudget = new FiberQuantumBudget(
            plan.CompilerOptions.MaxInternalInstructionsPerQuantum);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LifecycleMachine.TerminalStatuses.Contains(instance.Status))
            {
                return;
            }

            var execution = ScopeReducer.ReconcileForEachAdmission(
                plan,
                activeExecution ?? throw new InvalidOperationException(
                    "The structured execution state is not initialized."),
                maxConcurrentExecutionPathsPerInstance);
            execution = FiberScheduler.ApplyPathCeiling(
                execution,
                maxConcurrentExecutionPathsPerInstance);
            activeExecution = execution;
            var selected = FiberScheduler.SelectNext(execution.Scheduler);
            if (selected is null)
            {
                ApplyDerivedStatus(execution, instance);
                var joinable = execution.Scopes.Values
                    .Where(scope => scope.Phase == ExecutionScopePhase.Joinable)
                    .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (joinable is null)
                {
                    var failedScope = execution.Scopes.Values
                        .Where(scope => scope.Phase == ExecutionScopePhase.Failed)
                        .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (failedScope is not null)
                    {
                        FailScope(execution, failedScope, instance);
                        return;
                    }

                    if (execution.Fibers.Values.Any(fiber => fiber.Phase == FiberPhase.Blocked))
                    {
                        return;
                    }

                    throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                        "Structured ephemeral execution has no runnable, blocked, or joinable work.");
                }

                activeExecution = MergeAndResume(plan, execution, joinable, instance);
                if (!LifecycleMachine.TerminalStatuses.Contains(instance.Status))
                {
                    ApplyDerivedStatus(activeExecution, instance);
                }

                quantumBudget.EndTurn();
                continue;
            }

            var fiber = execution.Fibers[selected.Value];
            var instruction = plan.GetInstruction(fiber.InstructionId);
            if (quantumBudget.ShouldRotate(fiber.Id, instruction.Kind))
            {
                activeExecution = await RotateFiberQuantumAsync(
                    execution,
                    fiber,
                    instruction,
                    instance).ConfigureAwait(false);
                quantumBudget.EndTurn();
                continue;
            }
            quantumBudget.Record(fiber.Id, instruction.Kind);
            if (instruction.Kind == CompiledInstructionKind.Step)
            {
                quantumBudget.EndTurn();
            }
            switch (instruction.Kind)
            {
                case CompiledInstructionKind.Init:
                    activeExecution = Advance(plan, execution, fiber, instruction);
                    break;
                case CompiledInstructionKind.Wait:
                {
                    if (instruction.Operation is null || instruction.EventContract is null)
                    {
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Compiled wait '{instruction.Path}' has no typed executable binding.");
                    }

                    try
                    {
                        activeExecution = await RegisterFiberWaitAsync(
                            plan,
                            execution,
                            fiber,
                            instruction,
                            instance,
                            instruction.EventContract,
                            ResolveWaitCorrelation(plan, execution, fiber, instance.State, instruction),
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        FailExecutionBoundary(execution, fiber, instruction, instance, exception);
                        return;
                    }

                    break;
                }
                case CompiledInstructionKind.Delay:
                {
                    var duration = instruction.DelayDuration ??
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Compiled delay '{instruction.Path}' has no duration.");
                    activeExecution = RegisterFiberDelay(
                        plan,
                        execution,
                        fiber,
                        instruction,
                        instance,
                        duration);
                    break;
                }
                case CompiledInstructionKind.Step:
                {
                    var step = await ExecuteStepAsync(
                        plan,
                        execution,
                        fiber,
                        instruction,
                        instance,
                        cancellationToken).ConfigureAwait(false);
                    activeExecution = step.State;
                    if (step.InstanceTerminated)
                    {
                        return;
                    }

                    break;
                }
                case CompiledInstructionKind.If:
                case CompiledInstructionKind.LoopCheck:
                {
                    if (instruction.Operation is not { } condition)
                    {
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Compiled condition '{instruction.Path}' has no typed executable binding.");
                    }

                    var conditionState = ResolveFiberState(
                        plan,
                        execution,
                        fiber,
                        instance.State);
                    var matched = StructuredInvocationCache.Invoke(condition, conditionState) as bool? ??
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Compiled condition '{instruction.Path}' did not return a Boolean value.");
                    var target = matched
                        ? instruction.NextInstructionId
                        : instruction.AlternateInstructionId;
                    activeExecution = MoveTo(execution, fiber, target, instruction);
                    break;
                }
                case CompiledInstructionKind.IfJoin:
                case CompiledInstructionKind.LoopBack:
                case CompiledInstructionKind.LoopExit:
                    activeExecution = Advance(plan, execution, fiber, instruction);
                    break;
                case CompiledInstructionKind.StartScope:
                    try
                    {
                        activeExecution = StartScope(
                            plan,
                            execution,
                            fiber,
                            instruction,
                            instance.State);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        FailExecutionBoundary(execution, fiber, instruction, instance, exception);
                        return;
                    }

                    break;
                case CompiledInstructionKind.BranchReturn:
                {
                    if (!HandleBranchReturn(plan, execution, fiber, instance))
                    {
                        return;
                    }

                    break;
                }
                case CompiledInstructionKind.End:
                {
                    if (instance.HasUnresolvedRuntimeWork)
                    {
                        FailExecutionBoundary(
                            execution,
                            fiber,
                            instruction,
                            instance,
                            new WorkflowLifecycleException(
                                "Workflow cannot complete with unresolved runtime work."));
                        return;
                    }

                    var end = definition.RootSequence.Children
                        .OfType<EndNode<TState>>()
                        .Single(node => node.NodeId == instruction.Path);
                    StructuredSerializedValue? output = null;
                    if (instruction.OutputSelector is { } outputSelector &&
                        instruction.OutputType is { } outputType &&
                        instruction.OutputSchemaIdentity is { } outputSchemaIdentity)
                    {
                        var projected = StructuredInvocationCache.Invoke(outputSelector, instance.State);
                        output = codec.Serialize(projected, outputType, outputSchemaIdentity);
                    }

                    var outcomeName = instruction.FixedOutcomeName ?? end.ResolveOutcome(instance.State);
                    var completed = FiberReducer.Complete(fiber);
                    var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                    {
                        [fiber.Id] = completed
                    };
                    activeExecution = execution with
                    {
                        Fibers = fibers,
                        Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
                    };
                    CancelAllStructuredWaits();
                    WorkflowLifecycleTransition.FireOrThrow(instance, LifecycleTrigger.Complete);
                    instance.Complete(outcomeName, timeProvider.GetUtcNow(), output);
                    return;
                }
                default:
                    throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                        $"Structured ephemeral instruction '{instruction.Kind}' at '{instruction.Path}' " +
                        "is not implemented by the in-memory Adapter.");
            }

            var boundaryExecution = ScopeReducer.ReconcileForEachAdmission(
                plan,
                activeExecution ?? throw new InvalidOperationException(
                    "The structured execution state is not initialized."),
                maxConcurrentExecutionPathsPerInstance);
            boundaryExecution = FiberScheduler.ApplyPathCeiling(
                boundaryExecution,
                maxConcurrentExecutionPathsPerInstance);
            activeExecution = boundaryExecution;
            if (!LifecycleMachine.TerminalStatuses.Contains(instance.Status) &&
                instruction.Kind is
                    CompiledInstructionKind.Wait or
                    CompiledInstructionKind.Delay or
                    CompiledInstructionKind.Step or
                    CompiledInstructionKind.StartScope or
                    CompiledInstructionKind.BranchReturn &&
                (instance.Status == global::OrcaCore.WorkflowInstanceStatus.Waiting ||
                    boundaryExecution.Scheduler.RunnableFiberIds.Count == 0))
            {
                ApplyDerivedStatus(boundaryExecution, instance);
            }
        }
    }

    private void ApplyDerivedStatus(
        StructuredExecutionState execution,
        WorkflowInstance<TState> instance)
    {
        var derived = ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Ephemeral, execution);
        if (derived.Failure is { } failure)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException($"{failure.Code}: {failure.Message}");
        }

        if (derived.Status is global::OrcaCore.WorkflowInstanceStatus.Running or global::OrcaCore.WorkflowInstanceStatus.Waiting)
        {
            instance.ApplyStructuredStatus(derived.Status.Value, timeProvider.GetUtcNow());
        }
    }

    private async ValueTask<StepTransition> ExecuteStepAsync(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance,
        CancellationToken cancellationToken)
    {
        StepResult result;
        var updatedFiber = fiber;
        var resumedEvent = resumedEvents.Remove(fiber.Id, out var envelope) ? envelope : null;
        if (!grantedStepLeases.Remove(fiber.Id, out var governanceLease) &&
            !governance.TryEnterStep(
                instruction.StepType,
                instruction.Policy.TransientPoolKey,
                out governanceLease))
        {
            return BlockForResourceGrant(state, fiber, instruction, instance);
        }

        await using var ownedGovernanceLease = governanceLease;
        var timedOut = 0;
        TaskCompletionSource? timeoutReached = null;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutTimer = instruction.Policy.Timeout is { } timeout
            ? timeProvider.CreateTimer(
                _ =>
                {
                    Interlocked.Exchange(ref timedOut, 1);
                    timeoutReached?.TrySetResult();
                    timeoutCancellation.Cancel();
                },
                null,
                timeout,
                Timeout.InfiniteTimeSpan)
            : null;
        if (timeoutTimer is not null)
        {
            timeoutReached = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        var executionToken = timeoutTimer is null ? cancellationToken : timeoutCancellation.Token;
        var maxAttempts = instruction.Policy.Retry?.MaxAttempts ?? 1;
        var attempt = checked(updatedFiber.RetryAttempt + 1);
        var operationId = StepOperationId.Parse(
            updatedFiber.LogicalOperationKey ?? instance.BeginStepOperation(instruction.Path).Value);
        updatedFiber = updatedFiber with { LogicalOperationKey = operationId.Value };
        var stepExecution = RuntimeStepContextFactory.CreateExecution(
            instance.InstanceId,
            operationId,
            attempt);
        var forEachItem = ResolveForEachItemContext(state, fiber);
        var stepStartedAt = timeProvider.GetUtcNow();
        instance.StartStep(instruction.Path, stepStartedAt, instruction.Policy.Timeout);
        using var stuckTimer = stuckStepThreshold is { } threshold
            ? timeProvider.CreateTimer(
                _ => instance.MarkStuckStep(instruction.Path, timeProvider.GetUtcNow()),
                null,
                threshold,
                Timeout.InfiniteTimeSpan)
            : null;
        try
        {
            var step = ResolveStep(instruction);
            if (fiber.OwningScopeId is null)
            {
                if (step is not IStep<TState> rootStep)
                {
                    throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                        $"Compiled root step '{instruction.Path}' does not implement " +
                        $"IStep<{typeof(TState).Name}>.");
            }

                var stateSchema = typeof(TState).AssemblyQualifiedName ?? typeof(TState).FullName!;
                var attemptState = plan.DetachedAttemptState
                    ? (TState)(codec.Deserialize(codec.Serialize(
                        instance.State,
                        typeof(TState),
                        stateSchema)) ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            "Root state deserialized to null before step execution."))
                    : instance.State;
                var stepContext = RuntimeStepContextFactory.Create(
                    attemptState,
                    stepExecution,
                    resumedEvent,
                    timeProvider,
                    forEachItem);
                var physicalAttempt = rootStep.ExecuteAsync(stepContext, executionToken).AsTask();
                if (timeoutReached is not null &&
                    await Task.WhenAny(physicalAttempt, timeoutReached.Task).ConfigureAwait(false) ==
                        timeoutReached.Task &&
                    !physicalAttempt.IsCompleted)
                {
                    ownedGovernanceLease.RetainUntil(physicalAttempt);
                    result = TimedOutStepResult(operationId, attempt, instruction);
                }
                else
                {
                    result = await physicalAttempt.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result is not StepResult.Failed && plan.DetachedAttemptState)
                    {
                        var committedState = (TState)(codec.Deserialize(codec.Serialize(
                            stepContext.State,
                            typeof(TState),
                            stateSchema)) ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                                "Root state deserialized to null after step execution."));
                        instance.ReplaceState(committedState);
                    }
                    else if (result is not StepResult.Failed &&
                             !ReferenceEquals(stepContext.State, instance.State))
                    {
                        instance.ReplaceState(stepContext.State);
                    }
                }
            }
            else
            {
                var branch = ResolveBranch(plan, state, fiber);
                var localPayload = new StructuredSerializedValue(
                    branch.Input.BranchStateType,
                    branch.Input.BranchStateSchemaIdentity,
                    updatedFiber.LocalStatePayload ??
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Branch state payload is missing."));
                var localState = codec.Deserialize(localPayload) ??
                    throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Branch state deserialized to null.");
                var physicalAttempt = StructuredInvocationCache.ExecuteStepAsync(
                    branch.Input.BranchStateType,
                    step,
                    localState,
                    stepExecution,
                    resumedEvent,
                    timeProvider,
                    forEachItem,
                    resourceLease: null,
                    cancellationToken: executionToken).AsTask();
                if (timeoutReached is not null &&
                    await Task.WhenAny(physicalAttempt, timeoutReached.Task).ConfigureAwait(false) ==
                        timeoutReached.Task &&
                    !physicalAttempt.IsCompleted)
                {
                    ownedGovernanceLease.RetainUntil(physicalAttempt);
                    result = TimedOutStepResult(operationId, attempt, instruction);
                }
                else
                {
                    var invocation = await physicalAttempt.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    result = invocation.Result;
                    if (result is not StepResult.Failed || !plan.DetachedAttemptState)
                    {
                        updatedFiber = updatedFiber with
                        {
                            LocalStatePayload = codec.Serialize(
                                invocation.State,
                                branch.Input.BranchStateType,
                                branch.Input.BranchStateSchemaIdentity).Payload
                        };
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (
            Volatile.Read(ref timedOut) == 1 &&
            !cancellationToken.IsCancellationRequested)
        {
            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
            result = TimedOutStepResult(operationId, attempt, instruction);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
            var failure = exception is TargetInvocationException { InnerException: { } inner }
                ? inner
                : exception;
            result = new StepResult.Failed(
                failure as OrcaCoreException ?? new WorkflowLifecycleException(failure.Message, failure));
        }

        if (result is StepResult.Failed failedResult &&
            attempt < maxAttempts &&
            IsRetryEligible(failedResult.Error))
        {
            return BlockForRetry(
                state,
                updatedFiber,
                instruction,
                instance,
                attempt,
                instruction.Policy.Retry?.Backoff ?? TimeSpan.Zero);
        }

        updatedFiber = updatedFiber with
        {
            RetryAttempt = 0,
            RetryNotBefore = null,
            LogicalOperationKey = null
        };

        var fibersWithState = new Dictionary<FiberId, FiberRecord>(state.Fibers)
        {
            [fiber.Id] = updatedFiber
        };
        state = state with { Fibers = fibersWithState };
        switch (result)
        {
            case StepResult.Completed:
            {
                var advanced = Advance(plan, state, updatedFiber, instruction);
                return new StepTransition(
                    advanced with
                    {
                        Scheduler = FiberScheduler.CompleteTurn(
                            advanced.Scheduler,
                            fiber.Id,
                            requeueSelected: true)
                    },
                    InstanceTerminated: false);
            }
            case StepResult.Failed failed:
            {
                var failure = FailureProvenance.Create(
                    plan,
                    state,
                    fiber,
                    instruction,
                    failed.Error.Code,
                    failed.Error.Message);
                if (fiber.OwningScopeId is { } scopeId)
                {
                    var scope = state.Scopes[scopeId];
                    var scopePlan = plan.GetScope(scope.ScopePlanId);
                    if (scope.Kind == CompiledScopeKind.ForEach)
                    {
                        var transition = ScopeReducer.RecordForEachTerminal(
                            state,
                            scopePlan,
                            scopeId,
                            fiber.Id,
                            resultPayload: null,
                            failure,
                            maxConcurrentExecutionPathsPerInstance);
                        state = transition.State;
                        CancelTerminalFiberWaits(state);
                        var updatedScope = state.Scopes[scopeId];
                        if (transition.ScopeBecameJoinable)
                        {
                            state = MergeAndResume(plan, state, updatedScope, instance);
                            return new StepTransition(state, InstanceTerminated: false);
                        }

                        if (updatedScope.Phase == ExecutionScopePhase.Running)
                        {
                            return new StepTransition(state, InstanceTerminated: false);
                        }
                    }
                    else
                    {
                        var transition = ScopeReducer.RecordChildTerminals(
                            state,
                            scopeId,
                            [ChildTerminalOutcome.Failed(fiber.Id, failure)]);
                        state = transition.State;
                        CancelTerminalFiberWaits(state);
                        if (transition.ScopeBecameJoinable)
                        {
                            state = MergeAndResume(plan, state, state.Scopes[scopeId], instance);
                            return new StepTransition(state, InstanceTerminated: false);
                        }

                        if (state.Scopes[scopeId].Phase == ExecutionScopePhase.Running)
                        {
                            return new StepTransition(state, InstanceTerminated: false);
                        }
                    }
                }
                else
                {
                    var failedFibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
                    {
                        [fiber.Id] = FiberReducer.Fail(updatedFiber, failure)
                    };
                    state = state with
                    {
                        Fibers = failedFibers,
                        Scheduler = FiberScheduler.RemoveRunnable(state.Scheduler, [fiber.Id])
                    };
                }

                CancelAllStructuredWaits();
                instance.Fail(new WorkflowErrorDetails(
                    failure.Code,
                    failure.Message,
                    instruction.Path,
                    timeProvider.GetUtcNow()));
                return new StepTransition(state, InstanceTerminated: true);
            }
            default:
            {
                if (StepResultWaitAccessor.TryGetWait(result, out var eventContract, out var correlationId))
                {
                    activeExecution = await RegisterFiberWaitAsync(
                        plan,
                        state,
                        updatedFiber,
                        instruction,
                        instance,
                        eventContract,
                        correlationId,
                        cancellationToken).ConfigureAwait(false);
                    return new StepTransition(
                        activeExecution,
                        LifecycleMachine.TerminalStatuses.Contains(instance.Status));
                }

                var exception = new NotSupportedException(
                    $"Step result '{result.GetType().Name}' is durable-only and is not supported " +
                    "by the structured ephemeral adapter.");
                CancelAllStructuredWaits();
                instance.Fail(new WorkflowErrorDetails(
                    exception.GetType().Name,
                    exception.Message,
                    instruction.Path,
                    timeProvider.GetUtcNow()));
                _ = instance.ToSnapshot();
                throw exception;
            }
        }
    }

    private static ForEachItemContext? ResolveForEachItemContext(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        if (fiber.OwningScopeId is not { } scopeId ||
            !execution.Scopes.TryGetValue(scopeId, out var scope) ||
            scope.Kind != CompiledScopeKind.ForEach)
        {
            return null;
        }

        var index = scope.ForEach?.ItemIndexByFiber.TryGetValue(fiber.Id, out var itemIndex) == true
            ? itemIndex
            : throw new InvalidOperationException(
                $"ForEach scope '{scopeId}' has no item index for fiber '{fiber.Id}'.");
        return new ForEachItemContext(index);
    }

    private static StepResult TimedOutStepResult(
        StepOperationId operationId,
        int attempt,
        CompiledInstruction instruction)
    {
        var timeout = instruction.Policy.Timeout ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                $"Compiled timed step '{instruction.Path}' has no timeout.");
        return new StepResult.Failed(
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.StepTimeout(
                operationId,
                attempt,
                timeout));
    }

    private static bool IsRetryEligible(OrcaCoreException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is WorkflowDefinitionException or
            WorkflowDeadlineExceededException or
            WorkflowWaitTimeoutException)
        {
            return false;
        }

        return !error.Code.StartsWith("SFE-AUTH-", StringComparison.Ordinal) &&
            !error.Code.StartsWith("SFE-TYPE-", StringComparison.Ordinal) &&
            !error.Code.StartsWith("WF-CANCEL", StringComparison.Ordinal) &&
            !error.Code.StartsWith("LEASE-", StringComparison.Ordinal);
    }

    private object ResolveStep(CompiledInstruction instruction)
    {
        if (instruction.StepType is { } stepType)
        {
            if (serviceProvider is null)
            {
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                    $"Named step '{stepType.FullName}' requires a host service provider.");
            }

            return serviceProvider.GetService(stepType) ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                $"Named step '{stepType.FullName}' is not registered in the host service provider.");
        }

        var factory = instruction.Operation ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
            $"Compiled step '{instruction.Path}' has no executable binding.");
        return StructuredInvocationCache.Invoke(factory) ?? throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
            $"Compiled step factory '{instruction.Path}' returned null.");
    }

    private static BranchId? BranchIdForFiber(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber)
    {
        if (fiber.OwningScopeId is null)
        {
            return null;
        }

        var branch = ResolveBranch(plan, state, fiber);
        return new BranchId(branch.Ordinal, branch.BranchId);
    }

    private async Task ResumeFiberAsync(
        FiberId fiberId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var execution = activeExecution ??
            throw new InvalidOperationException("The structured execution state is not initialized.");
        waitsByFiber.Remove(fiberId);
        if (!execution.Fibers.TryGetValue(fiberId, out var blocked) ||
            blocked.Phase != FiberPhase.Blocked)
        {
            return;
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [fiberId] = FiberReducer.Resume(blocked)
        };
        resumedEvents[fiberId] = envelope;
        activeExecution = execution with
        {
            Fibers = fibers,
            Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, [fiberId])
        };
        await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ResumeTimerFiberAsync(FiberId fiberId, CancellationToken cancellationToken)
    {
        var execution = activeExecution ??
            throw new InvalidOperationException("The structured execution state is not initialized.");
        if (!timersByFiber.Remove(fiberId, out var timer))
        {
            return;
        }

        if (!execution.Fibers.TryGetValue(fiberId, out var blocked) ||
            blocked.Phase != FiberPhase.Blocked ||
            blocked.Blocked?.Reason != FiberBlockedReason.Timer)
        {
            timersByFiber[fiberId] = timer;
            return;
        }

        activeExecution = execution with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
            {
                [fiberId] = FiberReducer.Resume(blocked)
            },
            Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, [fiberId])
        };
        try
        {
            await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            activeExecution = execution;
            timersByFiber[fiberId] = timer;
            throw;
        }
    }

    private void CancelTerminalFiberWaits(StructuredExecutionState state)
    {
        var instance = activeInstance ??
            throw new InvalidOperationException("The structured execution instance is not initialized.");
        foreach (var owned in waitsByFiber
                     .Where(pair => state.Fibers[pair.Key].Phase != FiberPhase.Blocked)
                     .ToArray())
        {
            instance.CancelWait(owned.Value);
            waitsByFiber.Remove(owned.Key);
            resumedEvents.Remove(owned.Key);
        }

        foreach (var owned in timersByFiber
                     .Where(pair => state.Fibers[pair.Key].Phase != FiberPhase.Blocked)
                     .ToArray())
        {
            instance.CancelTimer(owned.Value);
            timersByFiber.Remove(owned.Key);
        }
    }

    private void CancelAllStructuredWaits()
    {
        var instance = activeInstance ??
            throw new InvalidOperationException("The structured execution instance is not initialized.");
        foreach (var wait in waitsByFiber.Values)
        {
            instance.CancelWait(wait);
        }

        waitsByFiber.Clear();
        foreach (var timer in timersByFiber.Values)
        {
            instance.CancelTimer(timer);
        }

        timersByFiber.Clear();
        resumedEvents.Clear();
    }

    private void FailScope(
        StructuredExecutionState state,
        ExecutionScopeRecord scope,
        WorkflowInstance<TState> instance)
    {
        var failures = scope.ForEach is { } forEach
            ? forEach.Outcomes.Values
                .Where(outcome => outcome.Failure is not null)
                .OrderBy(outcome => outcome.Index)
                .Select(outcome => outcome.Failure!)
                .ToArray()
            : scope.ChildFiberIds
                .Select(childId => state.Fibers[childId].Failure)
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .ToArray();
        var provenance = FailureProvenance.ForScope(FailurePlan, state, scope);
        var failure = ScopeReducer.AggregateFailures(
            failures,
            provenance.Location,
            provenance.Occurrence);
        CancelAllStructuredWaits();
        instance.Fail(new WorkflowErrorDetails(
            failure.Code,
            failure.Message,
            scope.ScopePlanId.Value,
            timeProvider.GetUtcNow()));
    }

    private static CompiledBranchPlan ResolveBranch(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Branch instruction has no owning scope.");
        var scope = state.Scopes[scopeId];
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        if (scopePlan.Kind == CompiledScopeKind.ForEach)
        {
            return scopePlan.Branches.Single();
        }

        var ordinal = scope.ChildFiberIds
            .Select((childId, index) => (childId, index))
            .Single(candidate => candidate.childId == fiber.Id)
            .index;
        return scopePlan.Branches.Single(candidate => candidate.Ordinal == ordinal);
    }

    private StructuredExecutionState StartScope(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord parent,
        CompiledInstruction instruction,
        TState rootState)
    {
        var scopePlan = plan.GetScope(new ScopePlanId($"scope:{instruction.Path}"));
        var parentState = ResolveFiberState(plan, state, parent, rootState);
        if (scopePlan.Kind == CompiledScopeKind.ForEach)
        {
            return ScopeReducer.StartForEachScope(
                state,
                parent.Id,
                scopePlan,
                MaterializeForEachDescriptors(scopePlan, parentState),
                maxConcurrentExecutionPathsPerInstance).State;
        }

        var materializedInputs = scopePlan.Branches
            .OrderBy(branch => branch.Ordinal)
            .Select(branch => BranchInputMaterializer.Materialize(branch.Input, parentState))
            .ToArray();
        var started = ScopeReducer.StartScope(
            state,
            parent.Id,
            scopePlan);
        var fibers = new Dictionary<FiberId, FiberRecord>(started.State.Fibers);
        for (var index = 0; index < started.ChildFiberIds.Count; index++)
        {
            var childId = started.ChildFiberIds[index];
            fibers[childId] = fibers[childId] with
            {
                LocalStatePayload = materializedInputs[index].Payload.ToArray()
            };
        }

        return started.State with { Fibers = fibers };
    }

    private object ResolveFiberState(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        TState rootState)
    {
        if (fiber.OwningScopeId is null)
        {
            return rootState ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Structured root state is null.");
        }

        var branch = ResolveBranch(plan, state, fiber);
        return codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Branch state payload is missing."))) ??
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException("Branch state deserialized to null.");
    }

    private CorrelationId ResolveWaitCorrelation(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        TState rootState,
        CompiledInstruction instruction)
    {
        try
        {
            return StructuredInvocationCache.Invoke(
                    instruction.Operation ??
                        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                            $"Compiled wait '{instruction.Path}' has no selector."),
                    ResolveFiberState(plan, state, fiber, rootState)) as CorrelationId ??
                throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                    $"Compiled wait '{instruction.Path}' did not return a CorrelationId.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.DefinitionException(
                $"Compiled wait selector '{instruction.Path}' failed.",
                exception.InnerException);
        }
    }

    private sealed record StepTransition(
        StructuredExecutionState State,
        bool InstanceTerminated);

    private sealed record BranchTerminalTransition(
        StructuredExecutionState State,
        ScopeId ScopeId,
        bool ScopeBecameJoinable);
}
