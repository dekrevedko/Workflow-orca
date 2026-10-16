namespace OrcaCore.TestSupport;

/// <summary>
/// Records deterministic async signals for tests that need to prove a contender reached a seam.
/// </summary>
public sealed class AsyncSignalCounter
{
    private readonly object gate = new();
    private readonly Dictionary<int, TaskCompletionSource> waiters = [];
    private int count;

    /// <summary>
    /// Gets the number of recorded signals.
    /// </summary>
    public int Count
    {
        get
        {
            lock (gate)
            {
                return count;
            }
        }
    }

    /// <summary>
    /// Records one signal and releases waiters whose requested count has been reached.
    /// </summary>
    public void Signal()
    {
        int[] completedKeys;
        TaskCompletionSource[] completedWaiters;
        lock (gate)
        {
            count++;
            completedKeys = waiters.Keys
                .Where(key => count >= key)
                .ToArray();
            completedWaiters = completedKeys
                .Select(key => waiters[key])
                .ToArray();
            foreach (var key in completedKeys)
            {
                waiters.Remove(key);
            }
        }

        foreach (var waiter in completedWaiters)
        {
            waiter.TrySetResult();
        }
    }

    /// <summary>
    /// Waits until at least <paramref name="expectedCount" /> signals have been recorded.
    /// </summary>
    public async Task WaitForCountAsync(int expectedCount, CancellationToken cancellationToken = default)
    {
        if (expectedCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedCount),
                expectedCount,
                "Expected count must be positive.");
        }

        TaskCompletionSource waiter;
        lock (gate)
        {
            if (count >= expectedCount)
            {
                return;
            }

            if (!waiters.TryGetValue(expectedCount, out waiter!))
            {
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                waiters.Add(expectedCount, waiter);
            }
        }

        await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
