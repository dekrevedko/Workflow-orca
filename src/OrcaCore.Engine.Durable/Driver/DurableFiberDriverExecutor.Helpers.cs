using System.Diagnostics;
using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private TState Initialize(DurableDriverContext context)
    {
        var init = definition.RootSequence.Children.OfType<InitNode<TState>>().Single();
        object? input = null;
        if (context.Aggregate.StartInputPayload is { } payload &&
            context.Aggregate.StartInputContentType is { } contentType &&
            init.RehydrateInput is { } rehydrate)
        {
            input = rehydrate(
                new SerializedPayload(contentType, payload),
                context.Serializer);
        }

        return init.CreateState(input);
    }

    private async ValueTask<ExecutedStep> ExecuteStepAsync(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState rootState,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var factory = instruction.Operation ??
            throw new WorkflowDefinitionException(
                $"Compiled step '{instruction.Path}' has no executable binding.");
        object step;
        try
        {
            step = StructuredInvocationCache.Invoke(factory) ??
                throw new WorkflowDefinitionException(
                    $"Compiled step factory '{instruction.Path}' returned null.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new WorkflowDefinitionException(
                $"Compiled step factory '{instruction.Path}' failed.",
                exception.InnerException);
        }

        if (fiber.OwningScopeId is null)
        {
            if (step is not IStep<TState> rootStep)
            {
                throw new WorkflowDefinitionException(
                    $"Compiled root step '{instruction.Path}' does not implement " +
                    $"IStep<{typeof(TState).Name}>.");
            }

            var result = await rootStep.ExecuteAsync(
                new StepContext<TState>(rootState, resumedEvent, timeProvider),
                cancellationToken).ConfigureAwait(false);
            return new ExecutedStep(execution, fiber, result);
        }

        var branch = ResolveBranch(execution, fiber);
        var localPayload = new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw new WorkflowDefinitionException("Branch state payload is missing."));
        var localState = codec.Deserialize(localPayload) ??
            throw new WorkflowDefinitionException("Branch state deserialized to null.");
        StepResult branchResult;
        try
        {
            branchResult = await StructuredInvocationCache.ExecuteStepAsync(
                branch.Input.BranchStateType,
                step,
                localState,
                resumedEvent,
                timeProvider,
                cancellationToken).ConfigureAwait(false);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw new WorkflowDefinitionException(
                $"Compiled branch step '{instruction.Path}' failed.",
                exception.InnerException);
        }

        var updatedFiber = fiber with
        {
            LocalStatePayload = codec.Serialize(
                localState,
                branch.Input.BranchStateType,
                branch.Input.BranchStateSchemaIdentity).Payload
        };
        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [fiber.Id] = updatedFiber
        };
        return new ExecutedStep(execution with { Fibers = fibers }, updatedFiber, branchResult);
    }

    private async ValueTask<PolicyExecutedStep> ExecutePolicyStepAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        EventEnvelope? resumedEvent,
        CancellationToken cancellationToken)
    {
        var committedExecution = execution;
        var committedFiber = fiber;
        var committedState = context.Serializer.Serialize(state);
        DurableCommandRuntime.StepCancellationScope? stepCancellation = null;
        CancellationTokenSource? timeoutCancellation = null;
        ITimer? timeoutTimer = null;
        var timedOut = 0;
        try
        {
            if (instruction.Policy.CancellationEnabled)
            {
                stepCancellation = context.Processor.EnterStep(context.InstanceId, cancellationToken);
            }

            var policyToken = stepCancellation?.Token ?? cancellationToken;
            if (fiber.TimeoutDeadline is { } deadline)
            {
                var remaining = deadline - context.TimeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    return FailedPolicyStep(
                        context,
                        committedExecution,
                        committedFiber,
                        committedState,
                        $"Step '{instruction.Path}' timed out after {instruction.Policy.Timeout}.");
                }

                timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(policyToken);
                timeoutTimer = context.TimeProvider.CreateTimer(
                    _ =>
                    {
                        Interlocked.Exchange(ref timedOut, 1);
                        _ = timeoutCancellation.CancelAsync();
                    },
                    null,
                    remaining,
                    Timeout.InfiniteTimeSpan);
                policyToken = timeoutCancellation.Token;
            }

            try
            {
                var executed = await ExecuteStepAsync(
                    execution,
                    fiber,
                    instruction,
                    state,
                    resumedEvent,
                    context.TimeProvider,
                    policyToken).ConfigureAwait(false);
                if (executed.Result is not StepResult.Failed)
                {
                    return new PolicyExecutedStep(
                        executed.Execution,
                        executed.Fiber,
                        state,
                        executed.Result,
                        OperatorCancelled: false);
                }

                return new PolicyExecutedStep(
                    committedExecution,
                    committedFiber,
                    context.Serializer.Deserialize<TState>(committedState),
                    executed.Result,
                    OperatorCancelled: false);
            }
            catch (OperationCanceledException) when (
                stepCancellation?.OperatorCancellationRequested == true &&
                !cancellationToken.IsCancellationRequested)
            {
                return new PolicyExecutedStep(
                    committedExecution,
                    committedFiber,
                    context.Serializer.Deserialize<TState>(committedState),
                    new StepResult.Failed(new WorkflowLifecycleException("Step was cancelled by an operator.")),
                    OperatorCancelled: true);
            }
            catch (OperationCanceledException) when (
                Volatile.Read(ref timedOut) != 0 &&
                !cancellationToken.IsCancellationRequested)
            {
                return FailedPolicyStep(
                    context,
                    committedExecution,
                    committedFiber,
                    committedState,
                    $"Step '{instruction.Path}' timed out after {instruction.Policy.Timeout}.");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return FailedPolicyStep(
                    context,
                    committedExecution,
                    committedFiber,
                    committedState,
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }
        finally
        {
            timeoutTimer?.Dispose();
            timeoutCancellation?.Dispose();
            stepCancellation?.Dispose();
        }
    }

    private static PolicyExecutedStep FailedPolicyStep(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        SerializedPayload committedState,
        string message)
    {
        return new PolicyExecutedStep(
            execution,
            fiber,
            context.Serializer.Deserialize<TState>(committedState),
            new StepResult.Failed(new WorkflowLifecycleException(message)),
            OperatorCancelled: false);
    }

    private StructuredExecutionState StartScope(
        StructuredExecutionState execution,
        FiberRecord parent,
        CompiledInstruction instruction,
        TState rootState)
    {
        var scopePlan = plan.GetScope(new ScopePlanId($"scope:{instruction.Path}"));
        if (scopePlan.Kind == CompiledScopeKind.ForEach)
        {
            throw new WorkflowDefinitionException(
                "Durable ForEach must be rejected during compilation.");
        }

        var parentState = ResolveFiberState(execution, parent, rootState);
        var materializedInputs = scopePlan.Branches
            .OrderBy(branch => branch.Ordinal)
            .Select(branch => BranchInputMaterializer.Materialize(branch.Input, parentState, codec))
            .ToArray();
        var started = ScopeReducer.StartScope(
            execution,
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
        StructuredExecutionState execution,
        FiberRecord fiber,
        TState rootState)
    {
        if (fiber.OwningScopeId is null)
        {
            return rootState ??
                throw new WorkflowDefinitionException("Structured root state is null.");
        }

        var branch = ResolveBranch(execution, fiber);
        return codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw new WorkflowDefinitionException("Branch state payload is missing."))) ??
            throw new WorkflowDefinitionException("Branch state deserialized to null.");
    }

    private CorrelationId ResolveWaitCorrelation(
        StructuredExecutionState execution,
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
                    ResolveFiberState(execution, fiber, rootState)) as CorrelationId? ??
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

    private CompiledBranchPlan ResolveBranch(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw new WorkflowDefinitionException("Branch instruction has no owning scope.");
        var scope = execution.Scopes[scopeId];
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        var ordinal = scope.ChildFiberIds
            .Select((childId, index) => (childId, index))
            .Single(candidate => candidate.childId == fiber.Id)
            .index;
        return scopePlan.Branches.Single(candidate => candidate.Ordinal == ordinal);
    }

    private BranchTerminalTransition ReturnBranch(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw new WorkflowDefinitionException("BranchReturn was reached outside an execution scope.");
        var branch = ResolveBranch(execution, fiber);
        var localState = codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw new WorkflowDefinitionException("Branch state payload is missing.")));
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
            throw new WorkflowDefinitionException(
                "Branch return projection failed.",
                exception.InnerException);
        }

        var resultPayload = codec.Serialize(
            result,
            branch.Result.ResultType,
            branch.Result.ResultSchemaIdentity);
        EnsureSerializedResultSize(resultPayload.Payload);
        var returned = ScopeReducer.RecordBranchReturn(execution, fiber, resultPayload.Payload);
        return new BranchTerminalTransition(
            returned.State,
            scopeId,
            returned.ScopeBecameJoinable);
    }

    private (StructuredExecutionState Execution, TState State) MergeAndResume(
        StructuredExecutionState execution,
        ExecutionScopeRecord scope,
        TState rootState)
    {
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        var parent = execution.Fibers[scope.ParentFiberId];
        var parentState = ResolveFiberState(execution, parent, rootState);
        var materializedResults = scopePlan.Branches
            .OrderBy(branch => branch.Ordinal)
            .Where(branch => scope.Kind != CompiledScopeKind.WhenFirst ||
                scope.ChildFiberIds[branch.Ordinal] == scope.WinnerFiberId)
            .Select(branch =>
            {
                var childId = scope.ChildFiberIds[branch.Ordinal];
                var payload = scope.CommittedResults[childId] ??
                    throw new WorkflowDefinitionException(
                        "Committed branch result payload is missing.");
                return new MaterializedBranchResult(
                    branch.Id,
                    new StructuredSerializedValue(
                        branch.Result.ResultType,
                        branch.Result.ResultSchemaIdentity,
                        payload));
            })
            .ToArray();
        var replacementPayload = ScopeMergeAdapter.Execute(
            scopePlan,
            parentState,
            materializedResults,
            codec);
        var replacement = codec.Deserialize(replacementPayload) ??
            throw new WorkflowDefinitionException("Structured merge produced null parent state.");
        var nextRootState = rootState;
        if (scope.ParentFiberId == execution.RootFiberId)
        {
            if (replacement is not TState typedReplacement)
            {
                throw new WorkflowDefinitionException(
                    $"Structured merge did not produce '{typeof(TState).FullName}'.");
            }

            nextRootState = typedReplacement;
        }

        var merging = ScopeReducer.BeginMerge(execution, scope.Id);
        var completed = ScopeReducer.CompleteMerge(
            plan,
            merging,
            scope.Id,
            replacementPayload.Payload);
        return (completed, nextRootState);
    }

    private DurableCheckpointPayload BuildEnvelope(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations)
    {
        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            execution,
            plan,
            context.Serializer.Serialize(state),
            ownedObligations);
        var serialized = envelope.Serialize();
        if (serialized.Length > plan.CompilerOptions.MaxSerializedEnvelopeBytes)
        {
            throw new StructuredExecutionLimitException(
                StructuredExecutionLimitCodes.SerializedEnvelopeExceeded,
                $"Serialized durable execution envelope is {serialized.Length} bytes, exceeding the " +
                $"configured limit of {plan.CompilerOptions.MaxSerializedEnvelopeBytes} bytes.");
        }

        return new DurableCheckpointPayload
        {
            ContentType = DurableExecutionEnvelopeV2.ContentType,
            Payload = serialized
        };
    }

    private static StructuredExecutionState CompleteStepTurn(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        var advanced = MoveTo(execution, fiber, RequiredNext(instruction));
        return advanced with
        {
            Scheduler = FiberScheduler.CompleteTurn(
                advanced.Scheduler,
                fiber.Id,
                requeueSelected: true)
        };
    }

    private static StructuredExecutionState FailFiberAndAncestors(
        StructuredExecutionState execution,
        FiberRecord failedFiber,
        FiberFailure failure)
    {
        var current = failedFiber;
        while (current.OwningScopeId is { } scopeId)
        {
            execution = ScopeReducer.RecordChildTerminals(
                execution,
                scopeId,
                [ChildTerminalOutcome.Failed(current.Id, failure)]).State;
            var scope = execution.Scopes[scopeId];
            if (scope.Phase != ExecutionScopePhase.Failed)
            {
                return execution;
            }

            current = execution.Fibers[scope.ParentFiberId];
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [current.Id] = FiberReducer.Fail(current, failure)
        };
        return execution with
        {
            Fibers = fibers,
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [current.Id])
        };
    }

    private static StructuredExecutionState MoveTo(
        StructuredExecutionState execution,
        FiberRecord fiber,
        InstructionId target)
    {
        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [fiber.Id] = fiber with { InstructionId = target }
        };
        return execution with { Fibers = fibers };
    }

    private static InstructionId RequiredNext(CompiledInstruction instruction)
    {
        return instruction.NextInstructionId ??
            throw new WorkflowDefinitionException(
                $"Compiled instruction '{instruction.Path}' has no continuation target.");
    }

    private static bool IsQuiescentRootRollover(
        StructuredExecutionState execution,
        FiberRecord selected,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations)
    {
        var hasActiveDescendants = execution.Fibers.Values.Any(fiber =>
                fiber.Id != selected.Id &&
                fiber.Phase is FiberPhase.Runnable or FiberPhase.Blocked) ||
            execution.Scopes.Values.Any(scope => scope.Phase is not (
                ExecutionScopePhase.Completed or
                ExecutionScopePhase.Failed or
                ExecutionScopePhase.Cancelled));
        return selected.Id == execution.RootFiberId &&
            selected.OwningScopeId is null &&
            !hasActiveDescendants &&
            ownedObligations.Count == 0;
    }

    private static IReadOnlyList<ScopeId> FailedSagaScopes(StructuredExecutionState execution)
    {
        return execution.Scopes.Values
            .Where(scope => scope.Phase == ExecutionScopePhase.Failed)
            .Select(scope => scope.Id)
            .OrderBy(scopeId => scopeId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool RootFailed(StructuredExecutionState execution)
    {
        return execution.Fibers[execution.RootFiberId].Phase == FiberPhase.Failed;
    }

    private async Task<OwnedWaitRegistration> RegisterOwnedWaitAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        string eventName,
        CorrelationId correlationId,
        WaitMode mode,
        StreamVersion currentVersion,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var waitId = WaitId.New();
        var advanced = ClearResume(fiber) with
        {
            InstructionId = RequiredNext(instruction)
        };
        var blocked = FiberReducer.Block(
            advanced,
            FiberBlockedReason.Wait,
            waitId.ToString());
        execution = execution with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
            {
                [fiber.Id] = blocked
            },
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
        };
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        var waitSequence = AllocateRegistrationSequence(ref execution);
        ownedObligations.Add(new DurableOwnedObligationState
        {
            Kind = DurableOwnedObligationKind.Wait,
            ObligationId = waitId.ToString(),
            FiberId = fiber.Id.Value,
            ScopeId = fiber.OwningScopeId?.Value,
            RegistrationSequence = waitSequence
        });
        var registered = await context.Processor.ProcessAsync(
            new DurableWaitRegisteredCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                waitId,
                eventName,
                correlationId,
                mode,
                fiber.OwningScopeId?.Value)
            {
                WaitSequence = waitSequence,
                FiberId = fiber.Id,
                ScopeId = fiber.OwningScopeId,
                Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                ExpectedStreamVersion = currentVersion,
                ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
            },
            cancellationToken).ConfigureAwait(false);
        return new OwnedWaitRegistration(execution, registered);
    }

    private static StructuredExecutionState ReconcilePendingResumes(
        StructuredExecutionState execution,
        IList<DurableOwnedObligationState> ownedObligations,
        IReadOnlyList<DurablePendingResume> pendingResumes,
        DurableChildWorkflowState childState)
    {
        var pendingByWaitId = pendingResumes.ToDictionary(
            pending => pending.WaitId.ToString(),
            StringComparer.Ordinal);
        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers);
        var resumed = new List<FiberId>();
        for (var index = 0; index < ownedObligations.Count; index++)
        {
            var obligation = ownedObligations[index];
            if (obligation.Kind is not (
                    DurableOwnedObligationKind.Wait or
                    DurableOwnedObligationKind.PendingResume or
                    DurableOwnedObligationKind.Resource or
                    DurableOwnedObligationKind.ExternalJob) ||
                !pendingByWaitId.ContainsKey(obligation.ObligationId))
            {
                continue;
            }

            var fiberId = new FiberId(obligation.FiberId);
            if (!fibers.TryGetValue(fiberId, out var fiber) ||
                fiber.Phase != FiberPhase.Blocked ||
                fiber.Blocked?.ObligationId != obligation.ObligationId)
            {
                continue;
            }

            fibers[fiberId] = FiberReducer.Resume(fiber) with
            {
                ResumeFromWaitId = obligation.ObligationId
            };
            ownedObligations[index] = obligation.Kind == DurableOwnedObligationKind.ExternalJob
                ? obligation
                : obligation with { Kind = DurableOwnedObligationKind.PendingResume };
            resumed.Add(fiberId);
        }

        foreach (var obligation in ownedObligations.Where(candidate =>
                     candidate.Kind == DurableOwnedObligationKind.ChildGroup))
        {
            if (!Guid.TryParse(obligation.ObligationId, out var groupGuid))
            {
                continue;
            }

            var resumeToken = new EventId(groupGuid);
            if (!childState.RecordedParentResumeTokens.Contains(resumeToken) ||
                childState.ConsumedParentResumeTokens.Contains(resumeToken))
            {
                continue;
            }

            var fiberId = new FiberId(obligation.FiberId);
            if (!fibers.TryGetValue(fiberId, out var fiber) ||
                fiber.Phase != FiberPhase.Blocked ||
                fiber.Blocked != new FiberBlock(
                    FiberBlockedReason.ChildGroup,
                    obligation.ObligationId))
            {
                continue;
            }

            fibers[fiberId] = FiberReducer.Resume(fiber);
            resumed.Add(fiberId);
        }

        var resumedDistinct = resumed.Distinct().ToArray();
        return resumedDistinct.Length == 0
            ? execution
            : execution with
            {
                Fibers = fibers,
                Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, resumedDistinct)
            };
    }

    private static StructuredExecutionState RecoverCommittedSuspensions(
        StructuredExecutionState execution,
        IList<DurableOwnedObligationState> ownedObligations,
        DurableWaitState waitState,
        DurableTimerState timerState,
        CompiledWorkflowPlan plan,
        out bool recovered)
    {
        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers);
        var resumed = new List<FiberId>();
        recovered = false;
        for (var index = ownedObligations.Count - 1; index >= 0; index--)
        {
            var obligation = ownedObligations[index];
            var isCommittedResourceGrant = obligation.Kind == DurableOwnedObligationKind.Resource &&
                !waitState.HasWait(new WaitId(Guid.Parse(obligation.ObligationId)));
            var isFiredTimer = obligation.Kind == DurableOwnedObligationKind.Timer &&
                timerState.FindActive(new TimerId(Guid.Parse(obligation.ObligationId))) is null;
            if (!isCommittedResourceGrant && !isFiredTimer)
            {
                continue;
            }

            var fiberId = new FiberId(obligation.FiberId);
            if (!fibers.TryGetValue(fiberId, out var fiber) ||
                fiber.Phase != FiberPhase.Blocked ||
                fiber.Blocked is not { } blocked ||
                (isFiredTimer && blocked.Reason is not (
                    FiberBlockedReason.Timer or FiberBlockedReason.Retry)) ||
                (isCommittedResourceGrant && blocked.Reason != FiberBlockedReason.Resource) ||
                blocked.ObligationId != obligation.ObligationId)
            {
                continue;
            }

            var instruction = plan.GetInstruction(fiber.InstructionId);
            fibers[fiberId] = FiberReducer.Resume(fiber) with
            {
                InstructionId = blocked.Reason == FiberBlockedReason.Retry
                    ? fiber.InstructionId
                    : RequiredNext(instruction),
                RetryNotBefore = null
            };
            ownedObligations.RemoveAt(index);
            resumed.Add(fiberId);
            recovered = true;
        }

        return recovered
            ? execution with
            {
                Fibers = fibers,
                Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, resumed)
            }
            : execution;
    }

    private sealed record OwnedWaitRegistration(
        StructuredExecutionState Execution,
        DurableCommandResult Commit);

    private sealed record PolicyExecutedStep(
        StructuredExecutionState Execution,
        FiberRecord Fiber,
        TState State,
        StepResult Result,
        bool OperatorCancelled);

    private static EventEnvelope? TakeResumedEvent(
        FiberRecord fiber,
        IReadOnlyList<DurablePendingResume> pendingResumes,
        out WaitId? consumedWaitId)
    {
        consumedWaitId = null;
        if (fiber.ResumeFromWaitId is not { } waitId ||
            pendingResumes.FirstOrDefault(pending => pending.WaitId.ToString() == waitId) is not { } pending)
        {
            return null;
        }

        consumedWaitId = pending.WaitId;
        return new EventEnvelope
        {
            EventId = pending.MatchedEventId,
            EventName = pending.EventName ?? string.Empty,
            CorrelationId = pending.CorrelationId ?? new CorrelationId("(uncorrelated)"),
            BranchId = pending.BranchId,
            Payload = ToResumedPayload(pending.Payload),
            PayloadContentType = pending.PayloadContentType,
            OccurredAt = pending.MatchedAt
        };
    }

    private static object? ToResumedPayload(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (System.Text.Json.JsonException)
        {
            return payload;
        }
    }

    private static FiberRecord ClearResume(FiberRecord fiber)
    {
        return fiber with { ResumeFromWaitId = null };
    }

    private static FiberRecord ClearStepPolicyState(FiberRecord fiber)
    {
        return fiber with
        {
            RetryAttempt = 0,
            RetryNotBefore = null,
            LogicalOperationKey = null,
            TimeoutDeadline = null
        };
    }

    private static string LogicalOperationKey(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        return $"{execution.ContinueAsNewGeneration}:{fiber.Id.Value}:{instruction.Id.Value}";
    }

    private static IReadOnlyList<WaitId> ConsumedWaitIds(WaitId? waitId)
    {
        return waitId is { } consumed ? [consumed] : [];
    }

    private static void RemoveConsumedObligation(
        IList<DurableOwnedObligationState> ownedObligations,
        WaitId? waitId)
    {
        if (waitId is not { } consumed)
        {
            return;
        }

        var obligation = ownedObligations.FirstOrDefault(candidate =>
            candidate.ObligationId == consumed.ToString());
        if (obligation is not null)
        {
            ownedObligations.Remove(obligation);
        }
    }

    private static long AllocateRegistrationSequence(ref StructuredExecutionState execution)
    {
        var allocated = execution.NextRegistrationSequence;
        execution = execution with
        {
            NextRegistrationSequence = checked(allocated + 1)
        };
        return allocated;
    }

    private static TerminalFiberCleanup RemoveTerminalFiberObligations(
        StructuredExecutionState execution,
        IList<DurableOwnedObligationState> ownedObligations)
    {
        var terminalFiberIds = execution.Fibers.Values
            .Where(fiber => fiber.Phase is FiberPhase.Completed or FiberPhase.Failed or FiberPhase.Cancelled)
            .Select(fiber => fiber.Id)
            .ToHashSet();
        var cancelled = ownedObligations
            .Where(obligation => terminalFiberIds.Contains(new FiberId(obligation.FiberId)))
            .ToArray();
        foreach (var obligation in cancelled)
        {
            ownedObligations.Remove(obligation);
        }

        return new TerminalFiberCleanup(
            cancelled
                .Where(obligation => obligation.Kind == DurableOwnedObligationKind.Wait)
                .Select(obligation => new WaitId(Guid.Parse(obligation.ObligationId)))
                .ToArray(),
            cancelled
                .Where(obligation => obligation.Kind == DurableOwnedObligationKind.Timer)
                .Select(obligation => new TimerId(Guid.Parse(obligation.ObligationId)))
                .ToArray(),
            terminalFiberIds.ToArray());
    }

    private sealed record TerminalFiberCleanup(
        IReadOnlyList<WaitId> WaitIds,
        IReadOnlyList<TimerId> TimerIds,
        IReadOnlyList<FiberId> TerminalFiberIds);

    private static bool BudgetReached(
        DurableDriverContext context,
        int commands,
        Stopwatch elapsed)
    {
        return commands >= context.Budget.MaxCommandsPerSegment ||
            elapsed.Elapsed >= context.Budget.MaxSegmentDuration;
    }

    private static DurableSegmentResult Conflict(DurableCommandResult result)
    {
        return new DurableSegmentResult(
            DurableSegmentOutcome.Conflict,
            result.Message ?? $"Kernel command outcome {result.Outcome}.");
    }

    private static Task<DurableWorkflowAggregate> ReloadAggregateAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        return new DurableAggregateLoader(context.Processor.EventStore)
            .LoadAsync(context.InstanceId, cancellationToken);
    }

    private static async Task<DurableSegmentResult> ParkAsync(
        DurableDriverContext context,
        DurableParkReason reason,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        var result = await context.Processor.ProcessAsync(
            new DurableParkCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                reason,
                diagnostic,
                FailedAttemptCount: 1,
                context.Aggregate.StreamVersion)
            {
                ExpectedStreamVersion = context.Aggregate.StreamVersion
            },
            cancellationToken).ConfigureAwait(false);
        return result.Outcome == DurableCommandOutcome.Committed
            ? new DurableSegmentResult(DurableSegmentOutcome.Parked, diagnostic)
            : Conflict(result);
    }

    private sealed class DurableStructuredValueCodec(IWorkflowTypeSerializerRegistry registry) : IStructuredValueCodec
    {
        public StructuredSerializedValue Serialize(
            object? value,
            Type declaredType,
            string schemaIdentity)
        {
            return new StructuredSerializedValue(
                declaredType,
                schemaIdentity,
                registry.Serialize(value, declaredType));
        }

        public object? Deserialize(StructuredSerializedValue value)
        {
            return registry.Deserialize(value.Payload, value.DeclaredType);
        }
    }

    private sealed record ExecutedStep(
        StructuredExecutionState Execution,
        FiberRecord Fiber,
        StepResult Result);

    private sealed record BranchTerminalTransition(
        StructuredExecutionState State,
        ScopeId ScopeId,
        bool ScopeBecameJoinable);
}
