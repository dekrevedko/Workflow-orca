namespace OrcaCore.Core.Execution;

public sealed record FiberSchedulerState(
    IReadOnlyList<FiberId> RunnableFiberIds,
    FiberId? NextFiberId);

public static class FiberScheduler
{
    public static StructuredExecutionState ApplyPathCeiling(
        StructuredExecutionState state,
        int maxConcurrentExecutionPaths)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (maxConcurrentExecutionPaths <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutionPaths),
                maxConcurrentExecutionPaths,
                "The execution-path ceiling must be positive.");
        }

        var runnable = state.Fibers.Values
            .Where(fiber => fiber.Phase == FiberPhase.Runnable)
            .Select(fiber => fiber.Id)
            .ToHashSet();
        var admitted = state.Scheduler.RunnableFiberIds
            .Where(runnable.Contains)
            .Distinct()
            .Take(maxConcurrentExecutionPaths)
            .ToList();
        var known = admitted.ToHashSet();
        foreach (var fiberId in AuthoredRunnableOrder(state, runnable))
        {
            if (admitted.Count == maxConcurrentExecutionPaths)
            {
                break;
            }

            if (known.Add(fiberId))
            {
                admitted.Add(fiberId);
            }
        }

        var scheduler = new FiberSchedulerState(
            admitted,
            admitted.Count == 0 ? null : admitted[0]);
        return state with { Scheduler = scheduler };
    }

    public static FiberSchedulerState Create(IReadOnlyList<FiberId> fibersInAuthoredOrder)
    {
        ArgumentNullException.ThrowIfNull(fibersInAuthoredOrder);
        if (fibersInAuthoredOrder.Count != fibersInAuthoredOrder.Distinct().Count())
        {
            throw new ArgumentException("Runnable fiber identities must be unique.", nameof(fibersInAuthoredOrder));
        }

        var queue = fibersInAuthoredOrder.ToArray();
        return new FiberSchedulerState(queue, queue.Length == 0 ? null : queue[0]);
    }

    public static FiberId? SelectNext(FiberSchedulerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.NextFiberId;
    }

    public static FiberSchedulerState CompleteTurn(
        FiberSchedulerState state,
        FiberId selectedFiberId,
        bool requeueSelected,
        IReadOnlyList<FiberId>? createdInAuthoredOrder = null,
        IReadOnlyList<FiberId>? resumedTogether = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.NextFiberId != selectedFiberId)
        {
            throw new InvalidOperationException(
                $"Fiber '{selectedFiberId}' is not the scheduler's next fiber '{state.NextFiberId}'.");
        }

        var queue = state.RunnableFiberIds
            .Where(fiberId => fiberId != selectedFiberId)
            .ToList();
        AppendUnique(queue, createdInAuthoredOrder ?? []);
        AppendUnique(
            queue,
            (resumedTogether ?? []).OrderBy(fiberId => fiberId.Value, StringComparer.Ordinal));
        if (requeueSelected)
        {
            queue.Add(selectedFiberId);
        }

        return new FiberSchedulerState(queue, queue.Count == 0 ? null : queue[0]);
    }

    public static FiberSchedulerState EnqueueResumed(
        FiberSchedulerState state,
        IReadOnlyList<FiberId> resumedTogether)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(resumedTogether);

        var queue = state.RunnableFiberIds.ToList();
        AppendUnique(
            queue,
            resumedTogether.OrderBy(fiberId => fiberId.Value, StringComparer.Ordinal));
        return new FiberSchedulerState(queue, queue.Count == 0 ? null : queue[0]);
    }

    public static FiberSchedulerState EnqueueCreated(
        FiberSchedulerState state,
        IReadOnlyList<FiberId> createdInAuthoredOrder)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(createdInAuthoredOrder);

        var queue = state.RunnableFiberIds.ToList();
        AppendUnique(queue, createdInAuthoredOrder);
        return new FiberSchedulerState(queue, queue.Count == 0 ? null : queue[0]);
    }

    public static FiberSchedulerState RemoveRunnable(
        FiberSchedulerState state,
        IReadOnlyCollection<FiberId> fiberIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fiberIds);

        var removed = fiberIds.ToHashSet();
        var queue = state.RunnableFiberIds.Where(fiberId => !removed.Contains(fiberId)).ToArray();
        return new FiberSchedulerState(queue, queue.Length == 0 ? null : queue[0]);
    }

    private static void AppendUnique(List<FiberId> queue, IEnumerable<FiberId> additions)
    {
        var known = queue.ToHashSet();
        foreach (var fiberId in additions)
        {
            if (!known.Add(fiberId))
            {
                throw new InvalidOperationException(
                    $"Fiber '{fiberId}' is already present in the runnable queue.");
            }

            queue.Add(fiberId);
        }
    }

    private static IEnumerable<FiberId> AuthoredRunnableOrder(
        StructuredExecutionState state,
        IReadOnlySet<FiberId> runnable)
    {
        if (runnable.Contains(state.RootFiberId))
        {
            yield return state.RootFiberId;
        }

        var yielded = new HashSet<FiberId> { state.RootFiberId };
        foreach (var scope in state.Scopes.Values
                     .OrderBy(scope => scope.ScopeEntrySequence)
                     .ThenBy(scope => scope.ScopePlanId.Value, StringComparer.Ordinal)
                     .ThenBy(scope => scope.Id.Value, StringComparer.Ordinal))
        {
            foreach (var childId in scope.ChildFiberIds)
            {
                if (runnable.Contains(childId) && yielded.Add(childId))
                {
                    yield return childId;
                }
            }
        }

        foreach (var fiberId in runnable
                     .Where(yielded.Add)
                     .OrderBy(fiberId => fiberId.Value, StringComparer.Ordinal))
        {
            yield return fiberId;
        }
    }
}
