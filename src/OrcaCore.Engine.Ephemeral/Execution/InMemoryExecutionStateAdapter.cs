using System.Collections;
using System.Reflection;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
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
    Action<WorkflowInstanceSnapshot> onSnapshotCommitted,
    TimeSpan? stuckStepThreshold,
    int maxPendingEvents,
    int maxConsumedEventIds,
    int maxLifecycleEvents)
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
            throw new WorkflowDefinitionException(
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
        activePlan = definition.CompiledPlan;
        codec = new StructuredEphemeralValueCodec(activePlan.SerializerRegistry);
        activeExecution = StructuredExecutionState.Create(
            instanceId,
            generation: 0,
            activePlan.Instructions[0].Id);
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

            var execution = activeExecution ??
                throw new InvalidOperationException("The structured execution state is not initialized.");
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

                    throw new WorkflowDefinitionException(
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
                    if (instruction.Operation is null || string.IsNullOrWhiteSpace(instruction.EventName))
                    {
                        throw new WorkflowDefinitionException(
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
                            instruction.EventName,
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
                        throw new WorkflowDefinitionException(
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
                    if (instruction.Operation is not Func<TState, bool> condition)
                    {
                        throw new WorkflowDefinitionException(
                            $"Compiled condition '{instruction.Path}' has no typed executable binding.");
                    }

                    var target = condition(instance.State)
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
                    var end = definition.RootSequence.Children
                        .OfType<EndNode<TState>>()
                        .Single(node => node.NodeId == instruction.Path);
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
                    instance.Complete(end.ResolveOutcome(instance.State), timeProvider.GetUtcNow());
                    return;
                }
                default:
                    throw new WorkflowDefinitionException(
                        $"Structured ephemeral instruction '{instruction.Kind}' at '{instruction.Path}' " +
                        "is not implemented by the in-memory Adapter.");
            }

            var boundaryExecution = activeExecution ?? throw new InvalidOperationException(
                "The structured execution state is not initialized.");
            if (!LifecycleMachine.TerminalStatuses.Contains(instance.Status) &&
                instruction.Kind is
                    CompiledInstructionKind.Wait or
                    CompiledInstructionKind.Delay or
                    CompiledInstructionKind.Step or
                    CompiledInstructionKind.StartScope or
                    CompiledInstructionKind.BranchReturn &&
                (instance.Status == WorkflowStatus.Waiting ||
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
            throw new WorkflowDefinitionException($"{failure.Code}: {failure.Message}");
        }

        if (derived.Status is WorkflowStatus.Running or WorkflowStatus.Waiting)
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
        var factory = instruction.Operation ??
            throw new WorkflowDefinitionException(
                $"Compiled step '{instruction.Path}' has no executable binding.");
        StepResult result;
        var updatedFiber = fiber;
        var resumedEvent = resumedEvents.Remove(fiber.Id, out var envelope) ? envelope : null;
        if (!grantedStepLeases.Remove(fiber.Id, out var governanceLease) &&
            !governance.TryEnterStep(instruction.Policy.TransientPoolKey, out governanceLease))
        {
            return BlockForResourceGrant(state, fiber, instruction, instance);
        }

        await using var ownedGovernanceLease = governanceLease;
        var timedOut = 0;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutTimer = instruction.Policy.Timeout is { } timeout
            ? timeProvider.CreateTimer(
                _ =>
                {
                    Interlocked.Exchange(ref timedOut, 1);
                    timeoutCancellation.Cancel();
                },
                null,
                timeout,
                Timeout.InfiniteTimeSpan)
            : null;
        var executionToken = timeoutTimer is null ? cancellationToken : timeoutCancellation.Token;
        var maxAttempts = instruction.Policy.Retry?.MaxAttempts ?? 1;
        var attempt = checked(updatedFiber.RetryAttempt + 1);
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
            var step = StructuredInvocationCache.Invoke(factory) ??
                throw new WorkflowDefinitionException(
                    $"Compiled step factory '{instruction.Path}' returned null.");
            if (fiber.OwningScopeId is null)
            {
                if (step is not IStep<TState> rootStep)
                {
                    throw new WorkflowDefinitionException(
                        $"Compiled root step '{instruction.Path}' does not implement " +
                        $"IStep<{typeof(TState).Name}>.");
            }

                result = await rootStep.ExecuteAsync(
                    new StepContext<TState>(instance.State, resumedEvent, timeProvider),
                    executionToken).ConfigureAwait(false);
            }
            else
            {
                var branch = ResolveBranch(plan, state, fiber);
                var localPayload = new StructuredSerializedValue(
                    branch.Input.BranchStateType,
                    branch.Input.BranchStateSchemaIdentity,
                    updatedFiber.LocalStatePayload ??
                        throw new WorkflowDefinitionException("Branch state payload is missing."));
                var localState = codec.Deserialize(localPayload) ??
                    throw new WorkflowDefinitionException("Branch state deserialized to null.");
                result = await StructuredInvocationCache.ExecuteStepAsync(
                    branch.Input.BranchStateType,
                    step,
                    localState,
                    resumedEvent,
                    timeProvider,
                    executionToken).ConfigureAwait(false);
                updatedFiber = updatedFiber with
                {
                    LocalStatePayload = codec.Serialize(
                        localState,
                        branch.Input.BranchStateType,
                        branch.Input.BranchStateSchemaIdentity).Payload
                };
            }

            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
        }
        catch (OperationCanceledException) when (
            Volatile.Read(ref timedOut) == 1 &&
            !cancellationToken.IsCancellationRequested)
        {
            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
            var timeoutFailure = new TimeoutException(
                $"Step '{instruction.Path}' timed out after {instruction.Policy.Timeout}.");
            result = new StepResult.Failed(new OrcaCoreException(timeoutFailure.Message, timeoutFailure));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            instance.CompleteStep(instruction.Path, timeProvider.GetUtcNow());
            var failure = exception is TargetInvocationException { InnerException: { } inner }
                ? inner
                : exception;
            result = new StepResult.Failed(
                failure as OrcaCoreException ?? new OrcaCoreException(failure.Message, failure));
        }

        if (result is StepResult.Failed && attempt < maxAttempts)
        {
            return BlockForRetry(
                state,
                updatedFiber,
                instruction,
                instance,
                attempt,
                instruction.Policy.Retry?.Backoff ?? TimeSpan.Zero);
        }

        updatedFiber = updatedFiber with { RetryAttempt = 0, RetryNotBefore = null };

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
            case StepResult.Yield:
            {
                var yieldedFibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
                {
                    [fiber.Id] = updatedFiber with { YieldCount = checked(updatedFiber.YieldCount + 1) }
                };
                return new StepTransition(
                    state with
                    {
                        Fibers = yieldedFibers,
                        Scheduler = FiberScheduler.CompleteTurn(
                            state.Scheduler,
                            fiber.Id,
                            requeueSelected: true)
                    },
                    InstanceTerminated: false);
            }
            case StepResult.Failed failed:
            {
                var failure = new FiberFailure(failed.Error.GetType().Name, failed.Error.Message);
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
                            plan.CompilerOptions.MaxActiveFibers);
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
                        state = ScopeReducer.RecordChildTerminals(
                            state,
                            scopeId,
                            [ChildTerminalOutcome.Failed(fiber.Id, failure)]).State;
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
            case StepResult.WaitForEvent wait:
            {
                activeExecution = await RegisterFiberWaitAsync(
                    plan,
                    state,
                    updatedFiber,
                    instruction,
                    instance,
                    wait.EventName,
                    wait.CorrelationId,
                    cancellationToken).ConfigureAwait(false);
                return new StepTransition(
                    activeExecution,
                    LifecycleMachine.TerminalStatuses.Contains(instance.Status));
            }
            default:
            {
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
        timersByFiber.Remove(fiberId);
        if (!execution.Fibers.TryGetValue(fiberId, out var blocked) ||
            blocked.Phase != FiberPhase.Blocked ||
            blocked.Blocked?.Reason != FiberBlockedReason.Timer)
        {
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
        await RunUntilBoundaryAsync(cancellationToken).ConfigureAwait(false);
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
        var failure = scope.ForEach?.Outcomes.Values
            .Where(outcome => outcome.Failure is not null)
            .OrderBy(outcome => outcome.Index)
            .Select(outcome => outcome.Failure)
            .FirstOrDefault() ??
            scope.ChildFiberIds
                .Select(childId => state.Fibers[childId].Failure)
                .FirstOrDefault(candidate => candidate is not null) ??
            new FiberFailure("StructuredScopeFailed", $"Execution scope '{scope.Id}' failed.");
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
            throw new WorkflowDefinitionException("Branch instruction has no owning scope.");
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
                plan.CompilerOptions.MaxActiveFibers).State;
        }

        var materializedInputs = scopePlan.Branches
            .OrderBy(branch => branch.Ordinal)
            .Select(branch => BranchInputMaterializer.Materialize(branch.Input, parentState, codec))
            .ToArray();
        var started = ScopeReducer.StartScope(
            state,
            parent.Id,
            scopePlan,
            plan.CompilerOptions.MaxActiveFibers);
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
                throw new WorkflowDefinitionException("Structured root state is null.");
        }

        var branch = ResolveBranch(plan, state, fiber);
        return codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw new WorkflowDefinitionException("Branch state payload is missing."))) ??
            throw new WorkflowDefinitionException("Branch state deserialized to null.");
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
                        throw new WorkflowDefinitionException(
                            $"Compiled wait '{instruction.Path}' has no selector."),
                    ResolveFiberState(plan, state, fiber, rootState)) as CorrelationId? ??
                throw new WorkflowDefinitionException(
                    $"Compiled wait '{instruction.Path}' did not return a CorrelationId.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new WorkflowDefinitionException(
                $"Compiled wait selector '{instruction.Path}' failed.",
                exception.InnerException);
        }
    }

    private IReadOnlyList<ForEachItemDescriptor> MaterializeForEachDescriptors(
        CompiledScopePlan scopePlan,
        object parentState)
    {
        var forEach = scopePlan.ForEach ??
            throw new WorkflowDefinitionException("Compiled ForEach contract is missing.");
        if (parentState is not TState typedParent)
        {
            throw new WorkflowDefinitionException(
                $"ForEach parent state must be '{typeof(TState).FullName}'.");
        }

        object? items;
        try
        {
            items = StructuredInvocationCache.Invoke(
                forEach.ItemSelector,
                new ReadOnlyParentSnapshot<TState>(typedParent));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new WorkflowDefinitionException("ForEach item selection failed.", exception.InnerException);
        }

        var partitionMethod = forEach.Partitioner.GetType().GetMethod("Partition") ??
            throw new WorkflowDefinitionException("ForEach partitioner has no Partition method.");
        var partitions = partitionMethod.Invoke(forEach.Partitioner, [items]) as IEnumerable ??
            throw new WorkflowDefinitionException("ForEach partitioner returned no work descriptors.");
        var inputType = typeof(ForEachItemInput<>).MakeGenericType(forEach.ItemType);
        var branch = scopePlan.Branches.Single();
        var descriptors = new List<ForEachItemDescriptor>();
        foreach (var partition in partitions)
        {
            var partitionType = partition!.GetType();
            var index = (int)(partitionType.GetProperty("Index")?.GetValue(partition) ??
                throw new WorkflowDefinitionException("ForEach partition index is missing."));
            var partitionItems = partitionType.GetProperty("Items")?.GetValue(partition) ??
                throw new WorkflowDefinitionException("ForEach partition items are missing.");
            var input = Activator.CreateInstance(inputType, index, partitionItems) ??
                throw new WorkflowDefinitionException(
                    $"Could not create ForEach item input for index '{index}'.");
            object? itemState;
            try
            {
                itemState = StructuredInvocationCache.Invoke(forEach.ItemStateProjector, input);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw new WorkflowDefinitionException(
                    $"ForEach item-state projection failed for index '{index}'.",
                    exception.InnerException);
            }

            descriptors.Add(new ForEachItemDescriptor(
                index,
                codec.Serialize(
                    itemState,
                    branch.Input.BranchStateType,
                    branch.Input.BranchStateSchemaIdentity).Payload));
        }

        return descriptors.OrderBy(descriptor => descriptor.Index).ToArray();
    }

    private BranchTerminalTransition ReturnBranch(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw new WorkflowDefinitionException("BranchReturn was reached outside an execution scope.");
        var scope = state.Scopes[scopeId];
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        var branch = ResolveBranch(plan, state, fiber);
        var localPayload = new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ?? throw new WorkflowDefinitionException("Branch state payload is missing."));
        var localState = codec.Deserialize(localPayload);
        var snapshot = StructuredInvocationCache.CreateBranchSnapshot(
            branch.Result.BranchStateType,
            localState ?? throw new WorkflowDefinitionException("Branch state deserialized to null."));
        object? result;
        try
        {
            result = StructuredInvocationCache.Invoke(branch.Result.Projector, snapshot);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new WorkflowDefinitionException("Branch return projection failed.", exception.InnerException);
        }

        var resultPayload = codec.Serialize(
            result,
            branch.Result.ResultType,
            branch.Result.ResultSchemaIdentity);
        EnsureSerializedResultSize(plan, resultPayload.Payload);
        if (scope.Kind == CompiledScopeKind.ForEach)
        {
            var transition = ScopeReducer.RecordForEachTerminal(
                state,
                scopePlan,
                scopeId,
                fiber.Id,
                resultPayload.Payload,
                failure: null,
                plan.CompilerOptions.MaxActiveFibers);
            return new BranchTerminalTransition(
                transition.State,
                transition.ScopeId,
                transition.ScopeBecameJoinable);
        }

        var returned = ScopeReducer.RecordBranchReturn(state, fiber, resultPayload.Payload);
        return new BranchTerminalTransition(
            returned.State,
            returned.ScopeId,
            returned.ScopeBecameJoinable);
    }

    private sealed record StepTransition(
        StructuredExecutionState State,
        bool InstanceTerminated);

    private sealed record BranchTerminalTransition(
        StructuredExecutionState State,
        ScopeId ScopeId,
        bool ScopeBecameJoinable);
}
