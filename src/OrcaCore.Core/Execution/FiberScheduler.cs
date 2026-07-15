namespace OrcaCore.Core.Execution;

internal sealed record FiberSchedulerState(
    IReadOnlyList<FiberId> RunnableFiberIds,
    FiberId? NextFiberId);

internal static class FiberScheduler
{
    internal static FiberSchedulerState Create(IReadOnlyList<FiberId> fibersInAuthoredOrder)
    {
        ArgumentNullException.ThrowIfNull(fibersInAuthoredOrder);
        if (fibersInAuthoredOrder.Count != fibersInAuthoredOrder.Distinct().Count())
        {
            throw new ArgumentException("Runnable fiber identities must be unique.", nameof(fibersInAuthoredOrder));
        }

        var queue = fibersInAuthoredOrder.ToArray();
        return new FiberSchedulerState(queue, queue.Length == 0 ? null : queue[0]);
    }

    internal static FiberId? SelectNext(FiberSchedulerState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.NextFiberId;
    }

    internal static FiberSchedulerState CompleteTurn(
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

    internal static FiberSchedulerState EnqueueResumed(
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

    internal static FiberSchedulerState EnqueueCreated(
        FiberSchedulerState state,
        IReadOnlyList<FiberId> createdInAuthoredOrder)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(createdInAuthoredOrder);

        var queue = state.RunnableFiberIds.ToList();
        AppendUnique(queue, createdInAuthoredOrder);
        return new FiberSchedulerState(queue, queue.Count == 0 ? null : queue[0]);
    }

    internal static FiberSchedulerState RemoveRunnable(
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
}
