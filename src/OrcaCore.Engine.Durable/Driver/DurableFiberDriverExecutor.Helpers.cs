using System.Diagnostics;
using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
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
            context.Aggregate.StartInputContentType is { } contentType)
        {
            input = context.Serializer.Deserialize(
                new SerializedPayload(contentType, payload),
                init.InputType);
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
        JsonWorkflowPayloadSerializer serializer,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations,
        CancellationToken cancellationToken)
    {
        var operationId = StepOperationId.Parse(
            fiber.LogicalOperationKey ?? LogicalOperationKey(execution, fiber, instruction));
        var stepExecution = RuntimeStepContextFactory.CreateExecution(
            execution.InstanceId,
            operationId,
            fiber.RetryAttempt > 0 ? fiber.RetryAttempt : 1);
        var leaseContext = ResolveLeaseExecutionContext(fiber, ownedObligations);
        var forEachItem = ResolveForEachItemContext(execution, fiber);
        object step;
        try
        {
            step = ResolveStep(instruction);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Compiled step factory '{instruction.Path}' failed.",
                exception.InnerException);
        }

        if (fiber.OwningScopeId is null)
        {
            if (step is not IStep<TState> rootStep)
            {
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    $"Compiled root step '{instruction.Path}' does not implement " +
                    $"IStep<{typeof(TState).Name}>.");
            }

            var attemptState = plan.DetachedAttemptState
                ? serializer.Deserialize<TState>(serializer.Serialize(rootState))
                : rootState;
            var stepContext = RuntimeStepContextFactory.Create(
                attemptState,
                stepExecution,
                resumedEvent,
                timeProvider,
                forEachItem,
                resourceLease: leaseContext);
            var result = await rootStep.ExecuteAsync(stepContext, cancellationToken).ConfigureAwait(false);
            var nextState = result is StepResult.Failed
                ? rootState
                : plan.DetachedAttemptState
                    ? serializer.Deserialize<TState>(serializer.Serialize(stepContext.State))
                    : stepContext.State;
            return new ExecutedStep(execution, fiber, result, nextState);
        }

        var branch = ResolveBranch(execution, fiber);
        var localPayload = new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state payload is missing."));
        var localState = codec.Deserialize(localPayload) ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state deserialized to null.");
        StepResult branchResult;
        try
        {
            var invocation = await StructuredInvocationCache.ExecuteStepAsync(
                branch.Input.BranchStateType,
                step,
                localState,
                stepExecution,
                resumedEvent,
                timeProvider,
                forEachItem,
                leaseContext,
                cancellationToken).ConfigureAwait(false);
            branchResult = invocation.Result;
            localState = invocation.State;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Compiled branch step '{instruction.Path}' failed.",
                exception.InnerException);
        }

        var updatedFiber = branchResult is StepResult.Failed && plan.DetachedAttemptState
            ? fiber
            : fiber with
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
        return new ExecutedStep(execution with { Fibers = fibers }, updatedFiber, branchResult, rootState);
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

    private object ResolveStep(CompiledInstruction instruction)
    {
        if (instruction.StepType is { } stepType)
        {
            if (serviceProvider is null)
            {
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    $"Named step '{stepType.FullName}' requires a host service provider.");
            }

            return serviceProvider.GetService(stepType) ?? throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Named step '{stepType.FullName}' is not registered in the host service provider.");
        }

        var factory = instruction.Operation ?? throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
            $"Compiled step '{instruction.Path}' has no executable binding.");
        return StructuredInvocationCache.Invoke(factory) ?? throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
            $"Compiled step factory '{instruction.Path}' returned null.");
    }

    private async ValueTask<PolicyExecutedStep> ExecutePolicyStepAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        EventEnvelope? resumedEvent,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations,
        DurableStepThrottleLease stepThrottleLease,
        CancellationToken cancellationToken)
    {
        var committedExecution = execution;
        var committedFiber = fiber;
        var committedState = context.Serializer.Serialize(state);
        var insideLease = ResolveLeaseExecutionContext(fiber, ownedObligations) is not null;
        DurableCommandRuntime.StepCancellationScope? stepCancellation = null;
        CancellationTokenSource? timeoutCancellation = null;
        ITimer? timeoutTimer = null;
        TaskCompletionSource? timeoutReached = null;
        var timedOut = 0;
        try
        {
            // Cancellation is an ordinary v1 instance capability, not an author-selected
            // step decorator. Every dispatched business step therefore participates.
            stepCancellation = context.Processor.EnterStep(context.InstanceId, cancellationToken);

            var policyToken = stepCancellation?.Token ?? cancellationToken;
            if (fiber.TimeoutDeadline is { } deadline)
            {
                var remaining = deadline - context.TimeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    return TimedOutPolicyStep(
                        context,
                        committedExecution,
                        committedFiber,
                        committedState,
                        instruction);
                }

                timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(policyToken);
                timeoutReached = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                timeoutTimer = context.TimeProvider.CreateTimer(
                    _ =>
                    {
                        Interlocked.Exchange(ref timedOut, 1);
                        timeoutReached.TrySetResult();
                        _ = timeoutCancellation.CancelAsync();
                    },
                    null,
                    remaining,
                    Timeout.InfiniteTimeSpan);
                policyToken = timeoutCancellation.Token;
            }

            try
            {
                var executionTask = ExecuteStepAsync(
                    execution,
                    fiber,
                    instruction,
                    state,
                    resumedEvent,
                    context.TimeProvider,
                    context.Serializer,
                    ownedObligations,
                    policyToken).AsTask();
                if (timeoutReached is not null)
                {
                    if (await DurablePolicyWinnerSelector.TimeoutWonAsync(
                            executionTask,
                            timeoutReached.Task)
                        .ConfigureAwait(false))
                    {
                        if (!insideLease)
                        {
                            stepThrottleLease.RetainUntil(executionTask);
                            ObserveLateAttempt(executionTask);
                        }
                        else
                        {
                            try
                            {
                                _ = await executionTask.ConfigureAwait(false);
                            }
                            catch
                            {
                                // The logical timeout owns the transition. The physical body is awaited
                                // only to enforce the no-overlap leased retry rule.
                            }
                        }

                        return TimedOutPolicyStep(
                            context,
                            committedExecution,
                            committedFiber,
                            committedState,
                            instruction);
                    }
                }

                var executed = await executionTask.ConfigureAwait(false);
                if (executed.Result is not StepResult.Failed)
                {
                    return new PolicyExecutedStep(
                        executed.Execution,
                        executed.Fiber,
                        executed.State,
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
                return TimedOutPolicyStep(
                    context,
                    committedExecution,
                    committedFiber,
                    committedState,
                    instruction);
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

    private static PolicyExecutedStep TimedOutPolicyStep(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        SerializedPayload committedState,
        CompiledInstruction instruction)
    {
        var timeout = instruction.Policy.Timeout ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Compiled timed step '{instruction.Path}' has no timeout.");
        var operationId = StepOperationId.Parse(
            fiber.LogicalOperationKey ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Compiled timed step '{instruction.Path}' has no persisted operation ID."));
        var exception = global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.StepTimeout(
            operationId,
            fiber.RetryAttempt > 0 ? fiber.RetryAttempt : 1,
            timeout);
        return new PolicyExecutedStep(
            execution,
            fiber,
            context.Serializer.Deserialize<TState>(committedState),
            new StepResult.Failed(exception),
            OperatorCancelled: false);
    }

    private static void ObserveLateAttempt(Task<ExecutedStep> executionTask)
    {
        global::OrcaCore.Engine.Durable.Diagnostics.OrcaCoreDurableDiagnostics
            .TrackFencedBody(executionTask);
        _ = executionTask.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private StructuredExecutionState StartScope(
        StructuredExecutionState execution,
        FiberRecord parent,
        CompiledInstruction instruction,
        TState rootState)
    {
        var scopePlan = plan.GetScope(new ScopePlanId($"scope:{instruction.Path}"));
        var parentState = ResolveFiberState(execution, parent, rootState);
        if (scopePlan.Kind == CompiledScopeKind.ForEach)
        {
            return ScopeReducer.StartForEachScope(
                execution,
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
            execution,
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

    private IReadOnlyList<ForEachItemDescriptor> MaterializeForEachDescriptors(
        CompiledScopePlan scopePlan,
        object parentState)
    {
        var forEach = scopePlan.ForEach ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                "Compiled ForEach contract is missing.");
        if (parentState is not TState typedParent)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
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
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                "ForEach item selection failed.",
                exception.InnerException);
        }

        items = ForEachSnapshotMaterializer.Materialize(items, forEach.ItemType);
        var partitionMethod = forEach.Partitioner.GetType().GetMethod("Partition") ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                "ForEach partitioner has no Partition method.");
        var partitions = partitionMethod.Invoke(forEach.Partitioner, [items]) as System.Collections.IEnumerable ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                "ForEach partitioner returned no work descriptors.");
        var inputType = typeof(global::OrcaCore.Core.Building.ForEachItemInput<>)
            .MakeGenericType(forEach.ItemType);
        var branch = scopePlan.Branches.Single();
        var descriptors = new List<ForEachItemDescriptor>();
        foreach (var partition in partitions)
        {
            var partitionType = partition!.GetType();
            var index = (int)(partitionType.GetProperty("Index")?.GetValue(partition) ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    "ForEach partition index is missing."));
            var partitionItems = partitionType.GetProperty("Items")?.GetValue(partition) ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    "ForEach partition items are missing.");
            var input = Activator.CreateInstance(inputType, index, partitionItems) ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    $"Could not create ForEach item input for index '{index}'.");
            object? itemState;
            try
            {
                itemState = StructuredInvocationCache.Invoke(forEach.ItemStateProjector, input);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
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

    private object ResolveFiberState(
        StructuredExecutionState execution,
        FiberRecord fiber,
        TState rootState)
    {
        if (fiber.OwningScopeId is null)
        {
            return rootState ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Structured root state is null.");
        }

        var branch = ResolveBranch(execution, fiber);
        return codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state payload is missing."))) ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state deserialized to null.");
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
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            $"Compiled wait '{instruction.Path}' has no selector."),
                    ResolveFiberState(execution, fiber, rootState)) as CorrelationId ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    $"Compiled wait '{instruction.Path}' did not return a CorrelationId.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Compiled wait selector '{instruction.Path}' failed.",
                exception.InnerException);
        }
    }

    private CompiledBranchPlan ResolveBranch(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch instruction has no owning scope.");
        var scope = execution.Scopes[scopeId];
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

    private BranchTerminalTransition ReturnBranch(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        var scopeId = fiber.OwningScopeId ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("BranchReturn was reached outside an execution scope.");
        var resultPayload = fiber.ResultPayload ?? ProjectBranchResultPayload(execution, fiber);
        var scope = execution.Scopes[scopeId];
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        if (scope.Kind == CompiledScopeKind.ForEach)
        {
            var forEach = ScopeReducer.RecordForEachTerminal(
                execution,
                scopePlan,
                scopeId,
                fiber.Id,
                resultPayload,
                failure: null,
                maxConcurrentExecutionPathsPerInstance);
            return new BranchTerminalTransition(
                forEach.State,
                forEach.ScopeId,
                forEach.ScopeBecameJoinable);
        }

        var returned = ScopeReducer.RecordBranchReturn(execution, fiber, resultPayload);
        return new BranchTerminalTransition(
            returned.State,
            scopeId,
            returned.ScopeBecameJoinable);
    }

    private byte[] ProjectBranchResultPayload(
        StructuredExecutionState execution,
        FiberRecord fiber)
    {
        var branch = ResolveBranch(execution, fiber);
        var localState = codec.Deserialize(new StructuredSerializedValue(
            branch.Input.BranchStateType,
            branch.Input.BranchStateSchemaIdentity,
            fiber.LocalStatePayload ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state payload is missing.")));
        var snapshot = StructuredInvocationCache.CreateBranchSnapshot(
            branch.Result.BranchStateType,
            localState ?? throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Branch state deserialized to null."));
        object? result;
        try
        {
            result = StructuredInvocationCache.Invoke(branch.Result.Projector, snapshot);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                "Branch return projection failed.",
                exception.InnerException);
        }

        var resultPayload = codec.Serialize(
            result,
            branch.Result.ResultType,
            branch.Result.ResultSchemaIdentity);
        EnsureSerializedResultSize(resultPayload.Payload);
        return resultPayload.Payload;
    }

    private (StructuredExecutionState Execution, TState State) MergeAndResume(
        StructuredExecutionState execution,
        ExecutionScopeRecord scope,
        TState rootState)
    {
        var scopePlan = plan.GetScope(scope.ScopePlanId);
        var parent = execution.Fibers[scope.ParentFiberId];
        var parentState = ResolveFiberState(execution, parent, rootState);
        StructuredSerializedValue replacementPayload;
        if (scope.Kind == CompiledScopeKind.ForEach)
        {
            var runtime = scope.ForEach ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    "ForEach scope runtime state is missing.");
            replacementPayload = ScopeMergeAdapter.ExecuteForEach(
                scopePlan,
                parentState,
                runtime.Outcomes.Values.OrderBy(outcome => outcome.Index).ToArray());
        }
        else if (scope.Kind == CompiledScopeKind.WhenAllOutcomes)
        {
            var materializedOutcomes = scopePlan.Branches
                .OrderBy(branch => branch.Ordinal)
                .Select(branch =>
                {
                    var childId = scope.ChildFiberIds[branch.Ordinal];
                    var child = execution.Fibers[childId];
                    if (child.Failure is { } failure)
                    {
                        return new MaterializedBranchOutcome(branch.Id, Result: null, failure);
                    }

                    var payload = scope.CommittedResults[childId] ??
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            "Committed branch result payload is missing.");
                    return new MaterializedBranchOutcome(
                        branch.Id,
                        new StructuredSerializedValue(
                            branch.Result.ResultType,
                            branch.Result.ResultSchemaIdentity,
                            payload),
                        Failure: null);
                })
                .ToArray();
            replacementPayload = ScopeMergeAdapter.ExecuteOutcomes(
                scopePlan,
                parentState,
                materializedOutcomes);
        }
        else
        {
            var materializedResults = scopePlan.Branches
                .OrderBy(branch => branch.Ordinal)
                .Select(branch =>
                {
                    var childId = scope.ChildFiberIds[branch.Ordinal];
                    var payload = scope.CommittedResults[childId] ??
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            "Committed branch result payload is missing.");
                    return new MaterializedBranchResult(
                        branch.Id,
                        new StructuredSerializedValue(
                            branch.Result.ResultType,
                            branch.Result.ResultSchemaIdentity,
                            payload));
                })
                .ToArray();
            replacementPayload = ScopeMergeAdapter.Execute(
                scopePlan,
                parentState,
                materializedResults);
        }
        var replacement = codec.Deserialize(replacementPayload) ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException("Structured merge produced null parent state.");
        var nextRootState = rootState;
        if (scope.ParentFiberId == execution.RootFiberId)
        {
            if (replacement is not TState typedReplacement)
            {
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
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
        IReadOnlyList<DurableOwnedObligationState> ownedObligations,
        DurableWorkflowOutputState? output = null)
    {
        execution = ReconcileAdmission(execution);
        var envelope = DurableFiberEnvelopeMapper.ToEnvelope(
            execution,
            plan,
            context.Serializer.Serialize(state),
            ownedObligations,
            output);
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

    private StructuredExecutionState ReconcileAdmission(StructuredExecutionState execution)
    {
        execution = ScopeReducer.ReconcileForEachAdmission(
            plan,
            execution,
            maxConcurrentExecutionPathsPerInstance);
        return FiberScheduler.ApplyPathCeiling(
            execution,
            maxConcurrentExecutionPathsPerInstance);
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
        if (failedFiber.OwningScopeId is { } scopeId)
        {
            return ScopeReducer.RecordChildTerminals(
                execution,
                scopeId,
                [ChildTerminalOutcome.Failed(failedFiber.Id, failure)]).State;
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [failedFiber.Id] = FiberReducer.Fail(failedFiber, failure)
        };
        return execution with
        {
            Fibers = fibers,
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [failedFiber.Id])
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
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
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
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        WaitMode mode,
        StreamVersion currentVersion,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var waitId = WaitId.Parse(Guid.CreateVersion7().ToString());
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
            InstructionId = instruction.Id.Value,
            AuthoredPath = instruction.Path,
            RegistrationSequence = waitSequence
        });
        var timeoutTimerId = instruction.WaitTimeout is not null ? TimerId.New() : (TimerId?)null;
        var now = context.TimeProvider.GetUtcNow();
        var registered = await context.Processor.ProcessAsync(
            new DurableWaitRegisteredCommand(
                CommandId.New(),
                context.InstanceId,
                now,
                waitId,
                eventContract.EventName.Value,
                correlationId,
                mode,
                fiber.OwningScopeId?.Value)
            {
                EventContractVersion = eventContract.Version.Value,
                WaitSequence = waitSequence,
                FiberId = fiber.Id,
                ScopeId = fiber.OwningScopeId,
                TimeoutTimerId = timeoutTimerId,
                TimeoutFireAt = instruction.WaitTimeout is { } timeout
                    ? now.Add(timeout)
                    : null,
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
        IReadOnlyList<DurablePendingResume> pendingResumes)
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
                    DurableOwnedObligationKind.Resource) ||
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
            ownedObligations[index] = obligation.ProtectionToken is not null
                ? obligation
                : obligation with { Kind = DurableOwnedObligationKind.PendingResume };
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
                !waitState.HasWait(WaitId.Parse(obligation.ObligationId));
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
            if (isCommittedResourceGrant && obligation.ProtectionToken is not null)
            {
                ownedObligations[index] = obligation with
                {
                    LeasePhase = nameof(DurableLeaseObligationPhase.Held)
                };
            }
            else
            {
                ownedObligations.RemoveAt(index);
            }
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

}
