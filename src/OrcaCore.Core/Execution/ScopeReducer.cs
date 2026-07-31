using OrcaCore.Core.Compilation;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Execution;

public static class ScopeReducer
{
    public static StructuredExecutionState BeginMerge(
        StructuredExecutionState state,
        ScopeId scopeId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Scopes.TryGetValue(scopeId, out var scope))
        {
            throw new InvalidOperationException($"Scope '{scopeId}' does not exist.");
        }

        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes)
        {
            [scopeId] = Transition(scope, ExecutionScopePhase.Merging)
        };
        return state with { Scopes = scopes };
    }

    public static StructuredExecutionState CompleteMerge(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        ScopeId scopeId,
        byte[]? parentStatePayload)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Scopes.TryGetValue(scopeId, out var scope))
        {
            throw new InvalidOperationException($"Scope '{scopeId}' does not exist.");
        }

        if (!state.Fibers.TryGetValue(scope.ParentFiberId, out var parent) ||
            parent.Phase != FiberPhase.Blocked ||
            parent.Blocked != new FiberBlock(FiberBlockedReason.Scope, scopeId.Value))
        {
            throw new InvalidOperationException(
                $"Scope '{scopeId}' does not own its expected blocked parent fiber.");
        }

        var exit = plan.GetInstruction(plan.GetScope(scope.ScopePlanId).ExitInstructionId);
        var continuation = exit.NextInstructionId ??
            throw new InvalidOperationException(
                $"Scope '{scopeId}' has no parent continuation after ScopeExit.");

        parent = FiberReducer.Resume(parent) with
        {
            InstructionId = continuation,
            LocalStatePayload = parentStatePayload?.ToArray()
        };
        _ = Transition(scope, ExecutionScopePhase.Completed);
        var completedScopeIds = DescendantScopeIds(state.Scopes, scopeId);
        var completedFiberIds = state.Fibers.Values
            .Where(fiber => fiber.OwningScopeId is { } owner && completedScopeIds.Contains(owner))
            .Select(fiber => fiber.Id)
            .ToHashSet();
        if (completedFiberIds.Any(fiberId =>
                state.Fibers[fiberId].Phase is FiberPhase.Runnable or FiberPhase.Blocked))
        {
            throw new InvalidOperationException(
                $"Scope '{scopeId}' cannot be pruned while it owns nonterminal fibers.");
        }

        var completedYields = completedFiberIds.Sum(fiberId => state.Fibers[fiberId].YieldCount);
        var completedRotations = completedFiberIds.Sum(fiberId =>
            state.Fibers[fiberId].ForcedRotationCount);
        var fibers = state.Fibers
            .Where(pair => !completedFiberIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        fibers[parent.Id] = parent;
        var scopes = state.Scopes
            .Where(pair => !completedScopeIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var scheduler = FiberScheduler.RemoveRunnable(state.Scheduler, completedFiberIds);
        scheduler = FiberScheduler.EnqueueResumed(scheduler, [parent.Id]);
        return state with
        {
            Fibers = fibers,
            Scopes = scopes,
            Scheduler = scheduler,
            CompletedYieldCount = checked(state.CompletedYieldCount + completedYields),
            CompletedForcedRotationCount = checked(
                state.CompletedForcedRotationCount + completedRotations)
        };
    }

    public static ScopeChildTransition RecordBranchReturn(
        StructuredExecutionState state,
        FiberRecord fiber,
        byte[]? resultPayload)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fiber);
        if (fiber.OwningScopeId is not { } scopeId)
        {
            throw new InvalidOperationException(
                $"Fiber '{fiber.Id}' does not belong to an active execution scope.");
        }

        return RecordChildTerminals(
            state,
            scopeId,
            [ChildTerminalOutcome.Succeeded(fiber.Id, resultPayload)]);
    }

    public static ScopeChildTransition RecordChildTerminals(
        StructuredExecutionState state,
        ScopeId scopeId,
        IReadOnlyList<ChildTerminalOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (!state.Scopes.TryGetValue(scopeId, out var scope) || scope.Phase != ExecutionScopePhase.Running)
        {
            throw new InvalidOperationException($"Scope '{scopeId}' is not running.");
        }

        if (outcomes.Count == 0 || outcomes.Select(outcome => outcome.FiberId).Distinct().Count() != outcomes.Count)
        {
            throw new ArgumentException("A terminal batch must contain unique child outcomes.", nameof(outcomes));
        }

        var ordinalByFiber = scope.ChildFiberIds
            .Select((fiberId, ordinal) => (fiberId, ordinal))
            .ToDictionary(candidate => candidate.fiberId, candidate => candidate.ordinal);
        if (outcomes.Any(outcome => !ordinalByFiber.ContainsKey(outcome.FiberId)))
        {
            throw new InvalidOperationException($"A terminal outcome is not owned by scope '{scopeId}'.");
        }

        var ordered = outcomes.OrderBy(outcome => ordinalByFiber[outcome.FiberId]).ToArray();
        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes);
        var results = new Dictionary<FiberId, byte[]?>(scope.CommittedResults);
        var becameJoinable = false;
        ExecutionScopeRecord updatedScope;

        if (scope.Kind == CompiledScopeKind.WhenFirst)
        {
            var winner = ordered[0];
            ApplyOutcome(fibers, results, winner);
            CancelNonterminalChildren(state, fibers, scopes, scope.ChildFiberIds, winner.FiberId);
            updatedScope = scope with
            {
                WinnerFiberId = winner.FiberId,
                CommittedResults = results
            };
            if (winner.Failure is null)
            {
                updatedScope = Transition(updatedScope, ExecutionScopePhase.Joinable);
                becameJoinable = true;
            }
            else
            {
                updatedScope = Transition(updatedScope, ExecutionScopePhase.Failed);
            }
        }
        else
        {
            foreach (var outcome in ordered)
            {
                ApplyOutcome(fibers, results, outcome);
            }

            updatedScope = scope with { CommittedResults = results };
            var allChildrenTerminal = scope.ChildFiberIds.All(childId =>
                fibers[childId].Phase is FiberPhase.Completed or FiberPhase.Failed or FiberPhase.Cancelled);
            if (allChildrenTerminal &&
                scope.Kind != CompiledScopeKind.WhenAllOutcomes &&
                scope.ChildFiberIds.Any(childId => fibers[childId].Phase == FiberPhase.Failed))
            {
                updatedScope = Transition(updatedScope, ExecutionScopePhase.Failed);
            }
            else if (allChildrenTerminal)
            {
                updatedScope = Transition(updatedScope, ExecutionScopePhase.Joinable);
                becameJoinable = true;
            }
        }

        scopes[scopeId] = updatedScope;
        var noLongerRunnable = state.Scheduler.RunnableFiberIds
            .Where(fiberId => fibers[fiberId].Phase != FiberPhase.Runnable)
            .ToArray();
        var scheduler = FiberScheduler.RemoveRunnable(state.Scheduler, noLongerRunnable);
        return new ScopeChildTransition(
            state with { Fibers = fibers, Scopes = scopes, Scheduler = scheduler },
            scopeId,
            becameJoinable);
    }

    public static ForEachScopeTransition StartForEachScope(
        StructuredExecutionState state,
        FiberId parentFiberId,
        CompiledScopePlan scopePlan,
        IReadOnlyList<ForEachItemDescriptor> descriptors,
        int maxConcurrentExecutionPaths = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(descriptors);
        var plan = scopePlan.ForEach ??
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' has no ForEach contract.");
        if (scopePlan.Kind != CompiledScopeKind.ForEach || scopePlan.Branches.Count != 1)
        {
            throw new InvalidOperationException($"Scope plan '{scopePlan.Id}' is not a dynamic ForEach scope.");
        }

        if (descriptors.Select(descriptor => descriptor.Index).Distinct().Count() != descriptors.Count ||
            descriptors.Any(descriptor => descriptor.Index < 0))
        {
            throw new ArgumentException("ForEach descriptors must have unique non-negative indices.", nameof(descriptors));
        }

        if (!state.Fibers.TryGetValue(parentFiberId, out var parent) || parent.Phase != FiberPhase.Runnable)
        {
            throw new InvalidOperationException($"Parent fiber '{parentFiberId}' is not runnable.");
        }

        if (maxConcurrentExecutionPaths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutionPaths),
                maxConcurrentExecutionPaths,
                "The execution-path ceiling must be positive.");
        }

        var ordered = descriptors.OrderBy(descriptor => descriptor.Index).ToArray();
        var nodeMaxConcurrency = plan.MaxConcurrency ?? int.MaxValue;
        var effectiveMaxConcurrency = Math.Min(
            nodeMaxConcurrency,
            maxConcurrentExecutionPaths);
        var entrySequence = parent.NextScopeEntrySequence;
        var scopeId = FiberIdentity.CreateScope(parentFiberId, scopePlan.Id, entrySequence);
        var admittedDescriptors = ordered
            .Take(Math.Min(effectiveMaxConcurrency, ordered.Length))
            .ToArray();
        var admittedIds = admittedDescriptors
            .Select(descriptor => FiberIdentity.CreateItem(scopeId, descriptor.Index))
            .ToArray();
        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
        {
            [parentFiberId] = FiberReducer.Block(parent, FiberBlockedReason.Scope, scopeId.Value) with
            {
                NextScopeEntrySequence = checked(entrySequence + 1)
            }
        };
        var itemIndexByFiber = new Dictionary<FiberId, int>();
        var template = scopePlan.Branches.Single();
        for (var index = 0; index < admittedDescriptors.Length; index++)
        {
            var descriptor = admittedDescriptors[index];
            var fiberId = admittedIds[index];
            fibers.Add(fiberId, CreateForEachFiber(fiberId, scopeId, template, descriptor));
            itemIndexByFiber.Add(fiberId, descriptor.Index);
        }

        var phase = ordered.Length == 0 ? ExecutionScopePhase.Joinable : ExecutionScopePhase.Running;
        var scope = new ExecutionScopeRecord(
            scopeId,
            scopePlan.Id,
            entrySequence,
            parent.OwningScopeId,
            parentFiberId,
            CompiledScopeKind.ForEach,
            phase,
            admittedIds,
            WinnerFiberId: null,
            new Dictionary<FiberId, byte[]?>())
        {
            ForEach = new ForEachRuntimeState(
                ordered,
                admittedDescriptors.Length,
                nodeMaxConcurrency,
                plan.JoinPolicy,
                plan.FailurePolicy,
                itemIndexByFiber,
                new Dictionary<int, ForEachTerminalOutcome>())
        };
        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes)
        {
            [scopeId] = scope
        };
        var scheduler = FiberScheduler.CompleteTurn(
            state.Scheduler,
            parentFiberId,
            requeueSelected: false,
            createdInAuthoredOrder: admittedIds);
        return new ForEachScopeTransition(
            state with { Fibers = fibers, Scopes = scopes, Scheduler = scheduler },
            scopeId,
            phase == ExecutionScopePhase.Joinable,
            admittedIds);
    }

    public static ForEachScopeTransition RecordForEachTerminal(
        StructuredExecutionState state,
        CompiledScopePlan scopePlan,
        ScopeId scopeId,
        FiberId fiberId,
        byte[]? resultPayload,
        FiberFailure? failure,
        int maxConcurrentExecutionPaths = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scopePlan);
        if (!state.Scopes.TryGetValue(scopeId, out var scope) ||
            scope.Phase != ExecutionScopePhase.Running ||
            scope.ForEach is not { } runtime)
        {
            throw new InvalidOperationException($"ForEach scope '{scopeId}' is not running.");
        }

        if (!runtime.ItemIndexByFiber.TryGetValue(fiberId, out var itemIndex) ||
            runtime.Outcomes.ContainsKey(itemIndex) ||
            !state.Fibers.TryGetValue(fiberId, out var fiber))
        {
            throw new InvalidOperationException(
                $"Fiber '{fiberId}' is not an active item of ForEach scope '{scopeId}'.");
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes);
        var results = new Dictionary<FiberId, byte[]?>(scope.CommittedResults);
        var outcomes = new Dictionary<int, ForEachTerminalOutcome>(runtime.Outcomes);
        if (failure is null)
        {
            fibers[fiberId] = FiberReducer.Complete(fiber, resultPayload);
            results[fiberId] = resultPayload?.ToArray();
            outcomes[itemIndex] = new ForEachTerminalOutcome(
                itemIndex,
                ForEachItemTerminalStatus.Succeeded,
                resultPayload?.ToArray(),
                null);
        }
        else
        {
            fibers[fiberId] = FiberReducer.Fail(fiber, failure);
            outcomes[itemIndex] = new ForEachTerminalOutcome(
                itemIndex,
                ForEachItemTerminalStatus.Failed,
                null,
                failure);
        }

        var scheduler = FiberScheduler.RemoveRunnable(state.Scheduler, [fiberId]);
        var nextRuntime = runtime with { Outcomes = outcomes };
        var nextScope = scope with { CommittedResults = results, ForEach = nextRuntime };
        var becameJoinable = false;
        var terminalFailure = failure is not null;
        if (runtime.JoinPolicy == ForEachJoinPolicy.WhenAny)
        {
            nextScope = nextScope with { WinnerFiberId = fiberId };
            (nextScope, fibers, scopes, scheduler) = CancelForEachResiduals(
                state,
                nextScope,
                fibers,
                scopes,
                scheduler);
            nextScope = Transition(
                nextScope,
                terminalFailure ? ExecutionScopePhase.Failed : ExecutionScopePhase.Joinable);
            becameJoinable = !terminalFailure;
        }
        else if (terminalFailure && runtime.FailurePolicy == ForEachFailurePolicy.FailFast)
        {
            (nextScope, fibers, scopes, scheduler) = CancelForEachResiduals(
                state,
                nextScope,
                fibers,
                scopes,
                scheduler);
            nextScope = Transition(nextScope, ExecutionScopePhase.Failed);
        }
        else if (outcomes.Count == runtime.Descriptors.Count)
        {
            var hasFailure = outcomes.Values.Any(outcome => outcome.Status == ForEachItemTerminalStatus.Failed);
            var target = hasFailure && runtime.FailurePolicy == ForEachFailurePolicy.WaitAllThenFail
                ? ExecutionScopePhase.Failed
                : ExecutionScopePhase.Joinable;
            nextScope = Transition(nextScope, target);
            becameJoinable = target == ExecutionScopePhase.Joinable;
        }

        IReadOnlyList<FiberId> admitted = [];
        if (nextScope.Phase == ExecutionScopePhase.Running)
        {
            (nextScope, fibers, scheduler, admitted) = AdmitForEachItems(
                nextScope,
                fibers,
                scheduler,
                scopePlan,
                maxConcurrentExecutionPaths);
        }

        scopes[scopeId] = nextScope;
        return new ForEachScopeTransition(
            state with { Fibers = fibers, Scopes = scopes, Scheduler = scheduler },
            scopeId,
            becameJoinable,
            admitted);
    }

    public static ForEachScopeTransition RecordForEachTerminalBatch(
        StructuredExecutionState state,
        CompiledScopePlan scopePlan,
        ScopeId scopeId,
        IReadOnlyList<ChildTerminalOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scopePlan);
        ArgumentNullException.ThrowIfNull(outcomes);
        if (outcomes.Count == 0 ||
            outcomes.Select(outcome => outcome.FiberId).Distinct().Count() != outcomes.Count)
        {
            throw new ArgumentException(
                "A ForEach terminal batch must contain unique child outcomes.",
                nameof(outcomes));
        }

        if (!state.Scopes.TryGetValue(scopeId, out var scope) ||
            scope.ForEach is not { JoinPolicy: ForEachJoinPolicy.WhenAny } runtime)
        {
            throw new InvalidOperationException(
                $"ForEach scope '{scopeId}' is not a running WhenAny scope.");
        }

        var winner = outcomes
            .OrderBy(outcome => runtime.ItemIndexByFiber.TryGetValue(outcome.FiberId, out var index)
                ? index
                : int.MaxValue)
            .First();
        if (!runtime.ItemIndexByFiber.ContainsKey(winner.FiberId))
        {
            throw new InvalidOperationException(
                $"A terminal outcome is not owned by ForEach scope '{scopeId}'.");
        }

        return RecordForEachTerminal(
            state,
            scopePlan,
            scopeId,
            winner.FiberId,
            winner.ResultPayload,
            winner.Failure);
    }

    public static StructuredExecutionState ReconcileForEachAdmission(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        int maxConcurrentExecutionPaths)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);
        if (maxConcurrentExecutionPaths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutionPaths),
                maxConcurrentExecutionPaths,
                "The execution-path ceiling must be positive.");
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes);
        var scheduler = state.Scheduler;
        foreach (var scopeId in scopes.Values
                     .Where(scope =>
                         scope.Kind == CompiledScopeKind.ForEach &&
                         scope.Phase == ExecutionScopePhase.Running)
                     .OrderBy(scope => scope.ScopeEntrySequence)
                     .ThenBy(scope => scope.Id.Value, StringComparer.Ordinal)
                     .Select(scope => scope.Id)
                     .ToArray())
        {
            var scope = scopes[scopeId];
            (scope, fibers, scheduler, _) = AdmitForEachItems(
                scope,
                fibers,
                scheduler,
                plan.GetScope(scope.ScopePlanId),
                maxConcurrentExecutionPaths);
            scopes[scopeId] = scope;
        }

        return state with { Fibers = fibers, Scopes = scopes, Scheduler = scheduler };
    }

    private static (ExecutionScopeRecord Scope, Dictionary<FiberId, FiberRecord> Fibers,
        FiberSchedulerState Scheduler, IReadOnlyList<FiberId> Admitted) AdmitForEachItems(
        ExecutionScopeRecord scope,
        Dictionary<FiberId, FiberRecord> fibers,
        FiberSchedulerState scheduler,
        CompiledScopePlan scopePlan,
        int maxConcurrentExecutionPaths)
    {
        var runtime = scope.ForEach!;
        var activeCount = scope.ChildFiberIds.Count(childId =>
            fibers[childId].Phase is FiberPhase.Runnable or FiberPhase.Blocked);
        var effectiveMaxConcurrency = Math.Min(
            runtime.MaxConcurrency,
            maxConcurrentExecutionPaths);
        var available = effectiveMaxConcurrency - activeCount;
        if (available <= 0 || runtime.NextAdmissionOffset >= runtime.Descriptors.Count)
        {
            return (scope, fibers, scheduler, []);
        }

        var descriptors = runtime.Descriptors
            .Skip(runtime.NextAdmissionOffset)
            .Take(available)
            .ToArray();
        var template = scopePlan.Branches.Single();
        var admitted = descriptors
            .Select(descriptor => FiberIdentity.CreateItem(scope.Id, descriptor.Index))
            .ToArray();
        var itemIndexByFiber = new Dictionary<FiberId, int>(runtime.ItemIndexByFiber);
        for (var index = 0; index < descriptors.Length; index++)
        {
            fibers.Add(
                admitted[index],
                CreateForEachFiber(admitted[index], scope.Id, template, descriptors[index]));
            itemIndexByFiber.Add(admitted[index], descriptors[index].Index);
        }

        scheduler = FiberScheduler.EnqueueCreated(scheduler, admitted);
        return (
            scope with
            {
                ChildFiberIds = [.. scope.ChildFiberIds, .. admitted],
                ForEach = runtime with
                {
                    NextAdmissionOffset = runtime.NextAdmissionOffset + descriptors.Length,
                    ItemIndexByFiber = itemIndexByFiber
                }
            },
            fibers,
            scheduler,
            admitted);
    }

    private static (ExecutionScopeRecord Scope, Dictionary<FiberId, FiberRecord> Fibers,
        Dictionary<ScopeId, ExecutionScopeRecord> Scopes, FiberSchedulerState Scheduler)
        CancelForEachResiduals(
        StructuredExecutionState state,
        ExecutionScopeRecord scope,
        Dictionary<FiberId, FiberRecord> fibers,
        Dictionary<ScopeId, ExecutionScopeRecord> scopes,
        FiberSchedulerState scheduler)
    {
        var runtime = scope.ForEach!;
        var outcomes = new Dictionary<int, ForEachTerminalOutcome>(runtime.Outcomes);
        var cancelledFiberIds = new HashSet<FiberId>();
        foreach (var node in ScopeOwnershipTraversal.PostOrder(state, scope.ChildFiberIds))
        {
            switch (node)
            {
                case OwnedFiberNode ownedFiber
                    when fibers[ownedFiber.FiberId].Phase is FiberPhase.Runnable or FiberPhase.Blocked:
                    fibers[ownedFiber.FiberId] = FiberReducer.Cancel(
                        fibers[ownedFiber.FiberId],
                        "foreach-residual-cancelled");
                    cancelledFiberIds.Add(ownedFiber.FiberId);
                    break;
                case OwnedScopeNode ownedScope
                    when scopes[ownedScope.ScopeId].Phase is not (
                        ExecutionScopePhase.Completed or
                        ExecutionScopePhase.Failed or
                        ExecutionScopePhase.Cancelled):
                    scopes[ownedScope.ScopeId] = Transition(
                        scopes[ownedScope.ScopeId],
                        ExecutionScopePhase.Cancelled);
                    break;
            }
        }

        foreach (var childId in scope.ChildFiberIds)
        {
            if (!cancelledFiberIds.Contains(childId))
            {
                continue;
            }

            var index = runtime.ItemIndexByFiber[childId];
            outcomes[index] = new ForEachTerminalOutcome(
                index,
                ForEachItemTerminalStatus.Cancelled,
                null,
                null);
        }

        for (var offset = runtime.NextAdmissionOffset; offset < runtime.Descriptors.Count; offset++)
        {
            var index = runtime.Descriptors[offset].Index;
            outcomes[index] = new ForEachTerminalOutcome(
                index,
                ForEachItemTerminalStatus.Cancelled,
                null,
                null);
        }

        scheduler = FiberScheduler.RemoveRunnable(scheduler, cancelledFiberIds);
        return (
            scope with
            {
                ForEach = runtime with
                {
                    NextAdmissionOffset = runtime.Descriptors.Count,
                    Outcomes = outcomes
                }
            },
            fibers,
            scopes,
            scheduler);
    }

    private static FiberRecord CreateForEachFiber(
        FiberId fiberId,
        ScopeId scopeId,
        CompiledBranchPlan template,
        ForEachItemDescriptor descriptor)
    {
        return new FiberRecord(
            fiberId,
            scopeId,
            template.Instructions[0],
            FiberPhase.Runnable,
            LoopIteration: 0,
            NextScopeEntrySequence: 0,
            descriptor.LocalStatePayload.ToArray(),
            ResultPayload: null,
            Blocked: null,
            Failure: null,
            CancellationReason: null);
    }

    private static void ApplyOutcome(
        Dictionary<FiberId, FiberRecord> fibers,
        Dictionary<FiberId, byte[]?> results,
        ChildTerminalOutcome outcome)
    {
        if (!fibers.TryGetValue(outcome.FiberId, out var fiber) ||
            fiber.Phase is not (FiberPhase.Runnable or FiberPhase.Blocked))
        {
            throw new InvalidOperationException(
                $"Fiber '{outcome.FiberId}' is not nonterminal for its terminal outcome.");
        }

        if (outcome.Failure is null)
        {
            if (fiber.Phase == FiberPhase.Blocked)
            {
                fiber = FiberReducer.Resume(fiber);
            }

            var completed = FiberReducer.Complete(fiber, outcome.ResultPayload);
            fibers[outcome.FiberId] = completed;
            results[outcome.FiberId] = completed.ResultPayload;
        }
        else
        {
            fibers[outcome.FiberId] = FiberReducer.Fail(fiber, outcome.Failure);
        }
    }

    private static void CancelNonterminalChildren(
        StructuredExecutionState state,
        Dictionary<FiberId, FiberRecord> fibers,
        Dictionary<ScopeId, ExecutionScopeRecord> scopes,
        IReadOnlyList<FiberId> childFiberIds,
        FiberId? except)
    {
        var cancelledRoots = childFiberIds.Where(childFiberId => childFiberId != except).ToArray();
        foreach (var node in ScopeOwnershipTraversal.PostOrder(state, cancelledRoots))
        {
            switch (node)
            {
                case OwnedFiberNode ownedFiber
                    when fibers[ownedFiber.FiberId].Phase is FiberPhase.Runnable or FiberPhase.Blocked:
                    fibers[ownedFiber.FiberId] = FiberReducer.Cancel(
                        fibers[ownedFiber.FiberId],
                        "scope-join-cancelled");
                    break;
                case OwnedScopeNode ownedScope
                    when scopes[ownedScope.ScopeId].Phase is not (
                        ExecutionScopePhase.Completed or
                        ExecutionScopePhase.Failed or
                        ExecutionScopePhase.Cancelled):
                    scopes[ownedScope.ScopeId] = Transition(
                        scopes[ownedScope.ScopeId],
                        ExecutionScopePhase.Cancelled);
                    break;
            }
        }
    }

    public static ExecutionScopeRecord Transition(
        ExecutionScopeRecord scope,
        ExecutionScopePhase target)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var allowed = (scope.Phase, target) switch
        {
            (ExecutionScopePhase.Created, ExecutionScopePhase.Running) => true,
            (ExecutionScopePhase.Running, ExecutionScopePhase.Joinable) => true,
            (ExecutionScopePhase.Running, ExecutionScopePhase.Failed) => true,
            (ExecutionScopePhase.Running, ExecutionScopePhase.Cancelled) => true,
            (ExecutionScopePhase.Joinable, ExecutionScopePhase.Merging) => true,
            (ExecutionScopePhase.Joinable, ExecutionScopePhase.Failed) => true,
            (ExecutionScopePhase.Joinable, ExecutionScopePhase.Cancelled) => true,
            (ExecutionScopePhase.Merging, ExecutionScopePhase.Completed) => true,
            (ExecutionScopePhase.Merging, ExecutionScopePhase.Failed) => true,
            (ExecutionScopePhase.Merging, ExecutionScopePhase.Cancelled) => true,
            _ => false
        };
        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Scope '{scope.Id}' cannot transition from '{scope.Phase}' to '{target}'.");
        }

        return scope with { Phase = target };
    }

    public static ScopeStartTransition StartScope(
        StructuredExecutionState state,
        FiberId parentFiberId,
        CompiledScopePlan scopePlan)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scopePlan);

        if (!state.Fibers.TryGetValue(parentFiberId, out var parent))
        {
            throw new InvalidOperationException($"Parent fiber '{parentFiberId}' does not exist.");
        }

        if (parent.Phase != FiberPhase.Runnable)
        {
            throw new InvalidOperationException(
                $"Parent fiber '{parentFiberId}' must be runnable to start a scope.");
        }

        if (scopePlan.Branches.Count == 0 || scopePlan.Branches.Any(branch => branch.Instructions.Count == 0))
        {
            throw new InvalidOperationException(
                $"Scope plan '{scopePlan.Id}' must contain non-empty branch instruction plans.");
        }

        var entrySequence = parent.NextScopeEntrySequence;
        var scopeId = FiberIdentity.CreateScope(parentFiberId, scopePlan.Id, entrySequence);
        var childFiberIds = scopePlan.Branches
            .OrderBy(branch => branch.Ordinal)
            .Select(branch => FiberIdentity.CreateChild(scopeId, branch.Id))
            .ToArray();

        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
        var blockedParent = FiberReducer.Block(
            parent,
            FiberBlockedReason.Scope,
            scopeId.Value) with
        {
            NextScopeEntrySequence = checked(entrySequence + 1)
        };
        fibers[parentFiberId] = blockedParent;

        var orderedBranches = scopePlan.Branches.OrderBy(branch => branch.Ordinal).ToArray();
        for (var index = 0; index < orderedBranches.Length; index++)
        {
            var branch = orderedBranches[index];
            var childId = childFiberIds[index];
            fibers.Add(childId, new FiberRecord(
                childId,
                scopeId,
                branch.Instructions[0],
                FiberPhase.Runnable,
                LoopIteration: 0,
                NextScopeEntrySequence: 0,
                LocalStatePayload: null,
                ResultPayload: null,
                Blocked: null,
                Failure: null,
                CancellationReason: null));
        }

        var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes)
        {
            [scopeId] = new ExecutionScopeRecord(
                scopeId,
                scopePlan.Id,
                entrySequence,
                parent.OwningScopeId,
                parentFiberId,
                scopePlan.Kind,
                ExecutionScopePhase.Running,
                childFiberIds,
                WinnerFiberId: null,
                new Dictionary<FiberId, byte[]?>())
        };
        var scheduler = FiberScheduler.CompleteTurn(
            state.Scheduler,
            parentFiberId,
            requeueSelected: false,
            createdInAuthoredOrder: childFiberIds);
        var next = state with { Scheduler = scheduler, Fibers = fibers, Scopes = scopes };
        return new ScopeStartTransition(next, scopeId, childFiberIds);
    }

    public static FiberFailure AggregateFailures(
        IReadOnlyList<FiberFailure> orderedFailures,
        AuthoredLocation? authoredLocation = null,
        FailureOccurrence? occurrence = null)
    {
        ArgumentNullException.ThrowIfNull(orderedFailures);
        authoredLocation ??= FailureProvenance.Location("workflow:$");
        occurrence ??= FailureProvenance.RootOccurrence();
        return orderedFailures.Count switch
        {
            0 => new FiberFailure(
                "SFE-JOIN-FAILED",
                "The structured scope failed without a recorded child failure.",
                authoredLocation: authoredLocation,
                occurrence: occurrence),
            1 => orderedFailures[0],
            _ => new FiberFailure(
                "SFE-JOIN-FAILED",
                $"{orderedFailures.Count} authored children failed.",
                orderedFailures,
                authoredLocation,
                occurrence)
        };
    }

    private static HashSet<ScopeId> DescendantScopeIds(
        IReadOnlyDictionary<ScopeId, ExecutionScopeRecord> scopes,
        ScopeId rootScopeId)
    {
        var descendants = new HashSet<ScopeId> { rootScopeId };
        var added = true;
        while (added)
        {
            added = false;
            foreach (var scope in scopes.Values)
            {
                if (scope.ParentScopeId is { } parent &&
                    descendants.Contains(parent) &&
                    descendants.Add(scope.Id))
                {
                    added = true;
                }
            }
        }

        return descendants;
    }

}
