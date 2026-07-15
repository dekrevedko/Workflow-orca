using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using System.Security.Cryptography;
using System.Text;

namespace OrcaCore.TestSupport.StructuredExecution;

public enum StructuredExecutionOperationKind
{
    Yield = 0,
    Block = 1,
    Complete = 2,
    ResumeTogether = 3,
    CrashReload = 4,
    DuplicateDelivery = 5,
    Fail = 6,
    Cancel = 7
}

public enum StructuredScopeScenarioKind
{
    Nested = 0,
    ForEach = 1
}

public sealed record StructuredExecutionOperation(
    StructuredExecutionOperationKind Kind,
    IReadOnlyList<string> FiberIds);

public sealed record StructuredExecutionScenario(
    int Seed,
    IReadOnlyList<string> AuthoredFiberIds,
    IReadOnlyList<StructuredExecutionOperation> Operations);

public sealed record StructuredExecutionComparisonResult(IReadOnlyList<string> Mismatches);

public sealed record StructuredScopeScenario(
    int Seed,
    StructuredScopeScenarioKind Kind,
    int WorkItemCount,
    int MaxConcurrency,
    IReadOnlyList<int> CompletionOrder);

public static class StructuredExecutionScenarioGenerator
{
    public static StructuredExecutionScenario Generate(
        int seed,
        int authoredFiberCount,
        int operationCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authoredFiberCount);
        ArgumentOutOfRangeException.ThrowIfNegative(operationCount);

        var random = new Random(seed);
        var authored = Enumerable.Range(0, authoredFiberCount)
            .Select(index => $"fiber:{index:D3}")
            .ToArray();
        var runnable = authored.ToList();
        var blocked = new HashSet<string>(StringComparer.Ordinal);
        var operations = new List<StructuredExecutionOperation>(operationCount);

        for (var index = 0; index < operationCount; index++)
        {
            if (index % 11 == 3)
            {
                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.CrashReload,
                    []));
                continue;
            }

            if (index % 13 == 7)
            {
                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.DuplicateDelivery,
                    []));
                continue;
            }

            if (blocked.Count > 0 && (runnable.Count == 0 || random.Next(4) == 0))
            {
                var resumed = blocked
                    .OrderBy(_ => random.Next())
                    .Take(random.Next(1, Math.Min(3, blocked.Count) + 1))
                    .ToArray();
                foreach (var fiberId in resumed.OrderBy(
                    StructuredExecutionReferenceIdentity.ChildFiberId,
                    StringComparer.Ordinal))
                {
                    blocked.Remove(fiberId);
                    runnable.Add(fiberId);
                }

                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.ResumeTogether,
                    resumed));
                continue;
            }

            if (runnable.Count == 0)
            {
                break;
            }

            var selected = runnable[0];
            var choice = random.Next(3);
            if (choice == 0)
            {
                runnable.RemoveAt(0);
                runnable.Add(selected);
                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.Yield,
                    [selected]));
            }
            else if (choice == 1)
            {
                runnable.RemoveAt(0);
                blocked.Add(selected);
                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.Block,
                    [selected]));
            }
            else
            {
                runnable.RemoveAt(0);
                operations.Add(new StructuredExecutionOperation(
                    StructuredExecutionOperationKind.Complete,
                    [selected]));
            }
        }

        return new StructuredExecutionScenario(seed, authored, operations);
    }

    public static StructuredExecutionScenario GenerateTerminal(
        int seed,
        int authoredFiberCount,
        StructuredExecutionOperationKind terminal)
    {
        if (terminal is not (StructuredExecutionOperationKind.Fail or StructuredExecutionOperationKind.Cancel))
        {
            throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "A terminal operation is required.");
        }

        if (authoredFiberCount < 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authoredFiberCount),
                authoredFiberCount,
                "At least four fibers are required for the terminal reference scenario.");
        }

        var authored = Enumerable.Range(0, authoredFiberCount)
            .Select(index => $"fiber:{index:D3}")
            .ToArray();
        return new StructuredExecutionScenario(
            seed,
            authored,
            [
                new StructuredExecutionOperation(StructuredExecutionOperationKind.Yield, [authored[0]]),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.Block, [authored[1]]),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.DuplicateDelivery, [authored[1]]),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.ResumeTogether, [authored[1]]),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.CrashReload, []),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.Complete, [authored[2]]),
                new StructuredExecutionOperation(StructuredExecutionOperationKind.CrashReload, []),
                new StructuredExecutionOperation(terminal, [authored[3]])
            ]);
    }
}

public static class StructuredScopeScenarioGenerator
{
    public static StructuredScopeScenario GenerateNested(int seed, int branchCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(branchCount, 2);
        return new StructuredScopeScenario(
            seed,
            StructuredScopeScenarioKind.Nested,
            branchCount,
            branchCount,
            CompletionOrder(seed, branchCount));
    }

    public static StructuredScopeScenario GenerateForEach(
        int seed,
        int itemCount,
        int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrency);
        return new StructuredScopeScenario(
            seed,
            StructuredScopeScenarioKind.ForEach,
            itemCount,
            maxConcurrency,
            CompletionOrder(seed, itemCount));
    }

    private static IReadOnlyList<int> CompletionOrder(int seed, int count)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count)
            .OrderBy(_ => random.Next())
            .ToArray();
    }
}

public static class StructuredExecutionComparisonHarness
{
    public static StructuredExecutionComparisonResult Compare(StructuredExecutionScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var mismatches = new List<string>();
        var referenceQueue = scenario.AuthoredFiberIds.ToList();
        var referencePhases = scenario.AuthoredFiberIds.ToDictionary(
            fiberId => fiberId,
            _ => ReferenceFiberPhase.Runnable,
            StringComparer.Ordinal);
        var scopePlan = CreateScopePlan(scenario.AuthoredFiberIds);
        var initial = StructuredExecutionState.Create(
            StructuredExecutionReferenceIdentity.InstanceId,
            generation: 0,
            new InstructionId("instruction:scope-start"));
        var started = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var actualIds = scenario.AuthoredFiberIds
            .Select((fiberId, index) => (fiberId, actualId: started.ChildFiberIds[index]))
            .ToDictionary(candidate => candidate.fiberId, candidate => candidate.actualId, StringComparer.Ordinal);
        var actualLabels = actualIds.ToDictionary(pair => pair.Value, pair => pair.Key);
        var actual = started.State;

        for (var index = 0; index < scenario.Operations.Count; index++)
        {
            var operation = scenario.Operations[index];
            try
            {
                ApplyReference(operation, referenceQueue, referencePhases);
                actual = ApplyActual(operation, actual, actualIds);
            }
            catch (Exception exception)
            {
                mismatches.Add($"Operation {index} ({operation.Kind}) threw: {exception.Message}");
                continue;
            }

            var actualQueue = actual.Scheduler.RunnableFiberIds
                .Select(fiberId => actualLabels[fiberId])
                .ToArray();
            if (!referenceQueue.SequenceEqual(actualQueue, StringComparer.Ordinal))
            {
                mismatches.Add(
                    $"Operation {index} ({operation.Kind}) queue: expected " +
                    $"[{string.Join(',', referenceQueue)}], actual [{string.Join(',', actualQueue)}].");
            }

            var expectedNext = referenceQueue.Count == 0 ? null : referenceQueue[0];
            var actualNext = actual.Scheduler.NextFiberId is { } nextFiberId
                ? actualLabels[nextFiberId]
                : null;
            if (!string.Equals(expectedNext, actualNext, StringComparison.Ordinal))
            {
                mismatches.Add(
                    $"Operation {index} ({operation.Kind}) next: expected '{expectedNext}', " +
                    $"actual '{actualNext}'.");
            }

            foreach (var pair in referencePhases)
            {
                var actualPhase = actual.Fibers[actualIds[pair.Key]].Phase;
                if (!PhaseMatches(pair.Value, actualPhase))
                {
                    mismatches.Add(
                        $"Operation {index} ({operation.Kind}) fiber '{pair.Key}': expected " +
                        $"'{pair.Value}', actual '{actualPhase}'.");
                }
            }

            var expectedScopePhase = referencePhases.Values.Any(phase => phase == ReferenceFiberPhase.Failed)
                ? ExecutionScopePhase.Failed
                : referencePhases.Values.Any(phase => phase == ReferenceFiberPhase.Cancelled)
                    ? ExecutionScopePhase.Cancelled
                    : referencePhases.Values.All(phase => phase == ReferenceFiberPhase.Completed)
                        ? ExecutionScopePhase.Joinable
                        : ExecutionScopePhase.Running;
            var actualScopePhase = actual.Scopes[started.ScopeId].Phase;
            if (actualScopePhase != expectedScopePhase)
            {
                mismatches.Add(
                    $"Operation {index} ({operation.Kind}) scope: expected '{expectedScopePhase}', " +
                    $"actual '{actualScopePhase}'.");
            }
        }

        return new StructuredExecutionComparisonResult(mismatches);
    }

    public static StructuredExecutionComparisonResult Compare(StructuredScopeScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        return scenario.Kind switch
        {
            StructuredScopeScenarioKind.Nested => CompareNested(scenario),
            StructuredScopeScenarioKind.ForEach => CompareForEach(scenario),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario.Kind, "Unknown scope scenario.")
        };
    }

    private static StructuredExecutionComparisonResult CompareNested(StructuredScopeScenario scenario)
    {
        var mismatches = new List<string>();
        var branchLabels = Enumerable.Range(0, scenario.WorkItemCount)
            .Select(index => $"nested:{index:D3}")
            .ToArray();
        var scopePlan = CreateScopePlan(branchLabels);
        var plan = CreateWorkflowPlan(scopePlan);
        var initial = StructuredExecutionState.Create(
            StructuredExecutionReferenceIdentity.InstanceId,
            generation: 0,
            new InstructionId("instruction:scope-start"));
        var outer = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        var replayedOuter = ScopeReducer.StartScope(initial, initial.RootFiberId, scopePlan);
        Check(outer.ScopeId == replayedOuter.ScopeId, "Outer scope identity changed on replay.", mismatches);
        Check(
            outer.ChildFiberIds.SequenceEqual(replayedOuter.ChildFiberIds),
            "Outer child identities changed on replay.",
            mismatches);

        var nestedParentId = outer.ChildFiberIds[0];
        var nested = ScopeReducer.StartScope(outer.State, nestedParentId, scopePlan);
        var replayedNested = ScopeReducer.StartScope(outer.State, nestedParentId, scopePlan);
        Check(nested.ScopeId == replayedNested.ScopeId, "Nested scope identity changed on replay.", mismatches);
        Check(
            nested.ChildFiberIds.SequenceEqual(replayedNested.ChildFiberIds),
            "Nested child identities changed on replay.",
            mismatches);
        Check(
            nested.State.Scopes[nested.ScopeId].ParentScopeId == outer.ScopeId,
            "Nested scope does not retain its parent scope.",
            mismatches);

        var state = nested.State;
        foreach (var ordinal in scenario.CompletionOrder)
        {
            var fiberId = nested.ChildFiberIds[ordinal];
            state = ScopeReducer.RecordBranchReturn(state, state.Fibers[fiberId], [(byte)ordinal]).State;
            state = Reload(state);
        }

        Check(
            state.Scopes[nested.ScopeId].Phase == ExecutionScopePhase.Joinable,
            "Nested scope did not become joinable after every child return.",
            mismatches);
        state = ScopeReducer.CompleteMerge(
            plan,
            ScopeReducer.BeginMerge(state, nested.ScopeId),
            nested.ScopeId,
            [7]);

        foreach (var ordinal in scenario.CompletionOrder)
        {
            var fiberId = outer.ChildFiberIds[ordinal];
            state = ScopeReducer.RecordBranchReturn(state, state.Fibers[fiberId], [(byte)ordinal]).State;
            state = Reload(state);
        }

        Check(
            state.Scopes[outer.ScopeId].Phase == ExecutionScopePhase.Joinable,
            "Outer scope did not become joinable after nested completion.",
            mismatches);
        state = ScopeReducer.CompleteMerge(
            plan,
            ScopeReducer.BeginMerge(state, outer.ScopeId),
            outer.ScopeId,
            [42]);

        var root = state.Fibers[state.RootFiberId];
        Check(
            state.Scopes.Count == 0,
            "Nested scenario retained a completed scope subtree.",
            mismatches);
        Check(
            state.Fibers.Count == 1,
            "Nested scenario retained completed child fibers.",
            mismatches);
        Check(
            root.LocalStatePayload?.SequenceEqual(new byte[] { 42 }) == true,
            "Root merge payload is not canonical.",
            mismatches);
        Check(
            state.Scheduler.RunnableFiberIds.SequenceEqual([state.RootFiberId]),
            "Nested scenario produced more than one parent continuation.",
            mismatches);
        return new StructuredExecutionComparisonResult(mismatches);
    }

    private static StructuredExecutionComparisonResult CompareForEach(StructuredScopeScenario scenario)
    {
        var mismatches = new List<string>();
        var plan = CreateForEachPlan(scenario.MaxConcurrency);
        var scopePlan = plan.Scopes.Single();
        var start = plan.Instructions.Single(instruction => instruction.Kind == CompiledInstructionKind.StartScope);
        var initial = StructuredExecutionState.Create(
            StructuredExecutionReferenceIdentity.InstanceId,
            generation: 0,
            start.Id);
        var descriptors = Enumerable.Range(0, scenario.WorkItemCount)
            .Select(index => new ForEachItemDescriptor(index, BitConverter.GetBytes(index)))
            .ToArray();
        var started = ScopeReducer.StartForEachScope(initial, initial.RootFiberId, scopePlan, descriptors);
        var replayed = ScopeReducer.StartForEachScope(initial, initial.RootFiberId, scopePlan, descriptors);
        Check(started.ScopeId == replayed.ScopeId, "ForEach scope identity changed on replay.", mismatches);
        Check(
            started.AdmittedFiberIds.SequenceEqual(replayed.AdmittedFiberIds),
            "ForEach admitted identities changed on replay.",
            mismatches);

        var state = started.State;
        var peakActive = 0;
        foreach (var decision in scenario.CompletionOrder)
        {
            var scope = state.Scopes[started.ScopeId];
            var active = scope.ChildFiberIds
                .Where(fiberId => state.Fibers[fiberId].Phase is FiberPhase.Runnable or FiberPhase.Blocked)
                .OrderBy(fiberId => scope.ForEach!.ItemIndexByFiber[fiberId])
                .ToArray();
            peakActive = Math.Max(peakActive, active.Length);
            if (active.Length == 0)
            {
                mismatches.Add("ForEach exhausted active items before every descriptor completed.");
                break;
            }

            var fiberId = active[decision % active.Length];
            var itemIndex = scope.ForEach!.ItemIndexByFiber[fiberId];
            state = ScopeReducer.RecordForEachTerminal(
                state,
                scopePlan,
                started.ScopeId,
                fiberId,
                BitConverter.GetBytes(itemIndex),
                failure: null).State;
            var outcomeCount = state.Scopes[started.ScopeId].ForEach!.Outcomes.Count;
            state = Reload(state);
            Check(
                state.Scopes[started.ScopeId].ForEach!.Outcomes.Count == outcomeCount,
                "Duplicate delivery changed committed ForEach outcomes.",
                mismatches);
        }

        var completedScope = state.Scopes[started.ScopeId];
        Check(peakActive <= scenario.MaxConcurrency, "ForEach exceeded its admission bound.", mismatches);
        Check(
            completedScope.Phase == ExecutionScopePhase.Joinable,
            "ForEach did not become joinable after every item completed.",
            mismatches);
        Check(
            completedScope.ForEach!.Outcomes.Keys.Order().SequenceEqual(
                Enumerable.Range(0, scenario.WorkItemCount)),
            "ForEach outcomes are not complete and item-index ordered.",
            mismatches);
        Check(
            completedScope.ChildFiberIds.All(fiberId => state.Fibers[fiberId].Phase == FiberPhase.Completed),
            "ForEach left a nonterminal item fiber.",
            mismatches);

        state = ScopeReducer.CompleteMerge(
            plan,
            ScopeReducer.BeginMerge(state, started.ScopeId),
            started.ScopeId,
            [42]);
        Check(
            state.Fibers[state.RootFiberId].LocalStatePayload?.SequenceEqual(new byte[] { 42 }) == true,
            "ForEach merge payload is not canonical.",
            mismatches);
        Check(
            state.Scheduler.RunnableFiberIds.SequenceEqual([state.RootFiberId]),
            "ForEach produced more than one parent continuation.",
            mismatches);
        return new StructuredExecutionComparisonResult(mismatches);
    }

    private static void ApplyReference(
        StructuredExecutionOperation operation,
        List<string> queue,
        Dictionary<string, ReferenceFiberPhase> phases)
    {
        switch (operation.Kind)
        {
            case StructuredExecutionOperationKind.Yield:
                RequireSelected(operation.FiberIds[0], queue);
                queue.RemoveAt(0);
                queue.Add(operation.FiberIds[0]);
                break;
            case StructuredExecutionOperationKind.Block:
                RequireSelected(operation.FiberIds[0], queue);
                queue.RemoveAt(0);
                phases[operation.FiberIds[0]] = ReferenceFiberPhase.Blocked;
                break;
            case StructuredExecutionOperationKind.Complete:
                RequireSelected(operation.FiberIds[0], queue);
                queue.RemoveAt(0);
                phases[operation.FiberIds[0]] = ReferenceFiberPhase.Completed;
                break;
            case StructuredExecutionOperationKind.ResumeTogether:
                foreach (var fiberId in operation.FiberIds.OrderBy(
                    StructuredExecutionReferenceIdentity.ChildFiberId,
                    StringComparer.Ordinal))
                {
                    if (phases[fiberId] != ReferenceFiberPhase.Blocked)
                    {
                        throw new InvalidOperationException($"Fiber '{fiberId}' is not blocked.");
                    }

                    phases[fiberId] = ReferenceFiberPhase.Runnable;
                    queue.Add(fiberId);
                }

                break;
            case StructuredExecutionOperationKind.CrashReload:
            case StructuredExecutionOperationKind.DuplicateDelivery:
                break;
            case StructuredExecutionOperationKind.Fail:
                RequireSelected(operation.FiberIds[0], queue);
                phases[operation.FiberIds[0]] = ReferenceFiberPhase.Failed;
                CancelActiveReferenceFibers(phases, operation.FiberIds[0]);
                queue.Clear();
                break;
            case StructuredExecutionOperationKind.Cancel:
                RequireSelected(operation.FiberIds[0], queue);
                CancelActiveReferenceFibers(phases, except: null);
                queue.Clear();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static StructuredExecutionState ApplyActual(
        StructuredExecutionOperation operation,
        StructuredExecutionState state,
        IReadOnlyDictionary<string, FiberId> ids)
    {
        switch (operation.Kind)
        {
            case StructuredExecutionOperationKind.Yield:
            {
                var fiberId = ids[operation.FiberIds[0]];
                var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
                {
                    [fiberId] = state.Fibers[fiberId] with
                    {
                        YieldCount = checked(state.Fibers[fiberId].YieldCount + 1)
                    }
                };
                return state with
                {
                    Fibers = fibers,
                    Scheduler = FiberScheduler.CompleteTurn(
                        state.Scheduler,
                        fiberId,
                        requeueSelected: true)
                };
            }
            case StructuredExecutionOperationKind.Block:
            {
                var fiberId = ids[operation.FiberIds[0]];
                var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
                {
                    [fiberId] = FiberReducer.Block(
                        state.Fibers[fiberId],
                        FiberBlockedReason.Wait,
                        $"wait:{fiberId}")
                };
                return state with
                {
                    Fibers = fibers,
                    Scheduler = FiberScheduler.CompleteTurn(
                        state.Scheduler,
                        fiberId,
                        requeueSelected: false)
                };
            }
            case StructuredExecutionOperationKind.Complete:
            {
                var fiberId = ids[operation.FiberIds[0]];
                return ScopeReducer.RecordBranchReturn(
                    state,
                    state.Fibers[fiberId],
                    resultPayload: [1]).State;
            }
            case StructuredExecutionOperationKind.ResumeTogether:
            {
                var resumedIds = operation.FiberIds.Select(fiberId => ids[fiberId]).ToArray();
                var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
                foreach (var fiberId in resumedIds)
                {
                    fibers[fiberId] = FiberReducer.Resume(fibers[fiberId]);
                }

                return state with
                {
                    Fibers = fibers,
                    Scheduler = FiberScheduler.EnqueueResumed(state.Scheduler, resumedIds)
                };
            }
            case StructuredExecutionOperationKind.CrashReload:
                return Reload(state);
            case StructuredExecutionOperationKind.DuplicateDelivery:
                return state;
            case StructuredExecutionOperationKind.Fail:
            {
                var fiberId = ids[operation.FiberIds[0]];
                var scopeId = state.Fibers[fiberId].OwningScopeId!.Value;
                return ScopeReducer.RecordChildTerminals(
                    state,
                    scopeId,
                    [ChildTerminalOutcome.Failed(fiberId, new FiberFailure("reference", "generated failure"))])
                    .State;
            }
            case StructuredExecutionOperationKind.Cancel:
            {
                var fiberId = ids[operation.FiberIds[0]];
                var scopeId = state.Fibers[fiberId].OwningScopeId!.Value;
                var scope = state.Scopes[scopeId];
                var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers);
                foreach (var childId in scope.ChildFiberIds)
                {
                    if (fibers[childId].Phase is FiberPhase.Runnable or FiberPhase.Blocked)
                    {
                        fibers[childId] = FiberReducer.Cancel(fibers[childId], "reference-cancelled");
                    }
                }

                var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(state.Scopes)
                {
                    [scopeId] = ScopeReducer.Transition(scope, ExecutionScopePhase.Cancelled)
                };
                return state with
                {
                    Fibers = fibers,
                    Scopes = scopes,
                    Scheduler = FiberScheduler.RemoveRunnable(state.Scheduler, scope.ChildFiberIds)
                };
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static CompiledScopePlan CreateScopePlan(IReadOnlyList<string> authoredFiberIds)
    {
        var branches = authoredFiberIds.Select((fiberId, ordinal) => new CompiledBranchPlan(
            new BranchPlanId($"branch:{fiberId}"),
            fiberId,
            ordinal,
            new CompiledBranchInputPlan(
                typeof(object),
                typeof(object),
                "object",
                "object",
                (Func<object, object>)(value => value)),
            new CompiledBranchResultPlan(
                typeof(object),
                typeof(object),
                "object",
                "object",
                (Func<object, object>)(value => value)),
            [new InstructionId($"instruction:{fiberId}:return")])).ToArray();
        return new CompiledScopePlan(
            new ScopePlanId("scope:reference"),
            CompiledScopeKind.WhenAll,
            typeof(object),
            branches,
            new CompiledMergePlan(
                CompiledMergeKind.WhenAll,
                typeof(object),
                typeof(object),
                "object",
                "object",
                (Func<object, object>)(value => value)),
            new InstructionId("instruction:scope:join"),
            new InstructionId("instruction:scope:exit"));
    }

    private static CompiledWorkflowPlan CreateWorkflowPlan(CompiledScopePlan scopePlan)
    {
        var continuation = new InstructionId("instruction:after-scope");
        return new CompiledWorkflowPlan(
            WorkflowExecutionMode.Ephemeral,
            new DefinitionId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            DefinitionVersion.Initial,
            "reference-nested",
            [
                new CompiledInstruction(
                    scopePlan.ExitInstructionId,
                    CompiledInstructionKind.ScopeExit,
                    "scope:exit",
                    CompiledPolicyPlan.Empty)
                {
                    NextInstructionId = continuation
                },
                new CompiledInstruction(
                    continuation,
                    CompiledInstructionKind.BranchReturn,
                    "scope:continuation",
                    CompiledPolicyPlan.Empty)
            ],
            [scopePlan],
            new HashSet<CompiledInstructionKind>
            {
                CompiledInstructionKind.ScopeExit,
                CompiledInstructionKind.BranchReturn
            });
    }

    private static CompiledWorkflowPlan CreateForEachPlan(int maxConcurrency)
    {
        return Workflow.Ephemeral<ReferenceForEachParent>(
                new DefinitionId(Guid.Parse("00000000-0000-0000-0000-000000000003")),
                DefinitionVersion.Initial)
            .Init<int[]>(items => new ReferenceForEachParent(items))
            .ForEach<int, ReferenceForEachItem, int>(
                parent => parent.Value.Items,
                WorkflowPartitioner<int>.Items(),
                item => new ReferenceForEachItem(item.Index),
                body => body.Return(item => item.Value.Index),
                ForEachJoinPolicy.WhenAll,
                ForEachFailurePolicy.ContinueWithPartialFailures,
                maxConcurrency,
                (parent, _) => parent.Value)
            .End()
            .Build()
            .CompiledPlan;
    }

    private static void Check(bool condition, string message, List<string> mismatches)
    {
        if (!condition)
        {
            mismatches.Add(message);
        }
    }

    private static bool PhaseMatches(ReferenceFiberPhase expected, FiberPhase actual)
    {
        return (expected, actual) switch
        {
            (ReferenceFiberPhase.Runnable, FiberPhase.Runnable) => true,
            (ReferenceFiberPhase.Blocked, FiberPhase.Blocked) => true,
            (ReferenceFiberPhase.Completed, FiberPhase.Completed) => true,
            (ReferenceFiberPhase.Failed, FiberPhase.Failed) => true,
            (ReferenceFiberPhase.Cancelled, FiberPhase.Cancelled) => true,
            _ => false
        };
    }

    private static void CancelActiveReferenceFibers(
        Dictionary<string, ReferenceFiberPhase> phases,
        string? except)
    {
        foreach (var fiberId in phases.Keys.ToArray())
        {
            if (!string.Equals(fiberId, except, StringComparison.Ordinal) &&
                phases[fiberId] is ReferenceFiberPhase.Runnable or ReferenceFiberPhase.Blocked)
            {
                phases[fiberId] = ReferenceFiberPhase.Cancelled;
            }
        }
    }

    private static StructuredExecutionState Reload(StructuredExecutionState state)
    {
        return state with
        {
            Fibers = state.Fibers.ToDictionary(pair => pair.Key, pair => pair.Value with
            {
                LocalStatePayload = pair.Value.LocalStatePayload?.ToArray(),
                ResultPayload = pair.Value.ResultPayload?.ToArray()
            }),
            Scopes = state.Scopes.ToDictionary(pair => pair.Key, pair => pair.Value with
            {
                ChildFiberIds = pair.Value.ChildFiberIds.ToArray(),
                CommittedResults = pair.Value.CommittedResults.ToDictionary(result => result.Key, result =>
                    result.Value?.ToArray()),
                ForEach = pair.Value.ForEach is null
                    ? null
                    : pair.Value.ForEach with
                    {
                        Descriptors = pair.Value.ForEach.Descriptors.Select(descriptor => descriptor with
                        {
                            LocalStatePayload = descriptor.LocalStatePayload.ToArray()
                        }).ToArray(),
                        ItemIndexByFiber = pair.Value.ForEach.ItemIndexByFiber.ToDictionary(),
                        Outcomes = pair.Value.ForEach.Outcomes.ToDictionary(outcome => outcome.Key, outcome =>
                            outcome.Value with { ResultPayload = outcome.Value.ResultPayload?.ToArray() })
                    }
            }),
            Scheduler = FiberScheduler.Create(state.Scheduler.RunnableFiberIds.ToArray())
        };
    }

    private static void RequireSelected(string fiberId, IReadOnlyList<string> queue)
    {
        if (queue.Count == 0 || !string.Equals(queue[0], fiberId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Fiber '{fiberId}' is not selected.");
        }
    }

    private enum ReferenceFiberPhase
    {
        Runnable,
        Blocked,
        Completed,
        Failed,
        Cancelled
    }

    private sealed record ReferenceForEachParent(IReadOnlyList<int> Items);

    private sealed record ReferenceForEachItem(int Index);
}

internal static class StructuredExecutionReferenceIdentity
{
    internal static InstanceId InstanceId { get; } = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"));

    internal static string ChildFiberId(string branchId)
    {
        var root = Hash("root", InstanceId.ToString(), "0");
        var scope = Hash("scope", root, "scope:reference", "0");
        return Hash("child", scope, $"branch:{branchId}");
    }

    private static string Hash(params string[] parts)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts))));
    }
}
