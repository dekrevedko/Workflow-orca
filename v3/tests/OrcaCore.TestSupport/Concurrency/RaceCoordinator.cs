namespace OrcaCore.TestSupport.Concurrency;

/// <summary>
/// Forces exactly two async callers to reach a gate before either proceeds, using deterministic
/// <see cref="TaskCompletionSource"/> coordination rather than sleeps or retry loops. A caller
/// that never gets a partner times out with <see cref="TimeoutException"/> instead of hanging.
/// </summary>
public sealed class RaceCoordinator(TimeSpan? timeout = null)
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);
    private int _arrivals;

    public async Task ArriveAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _arrivals) == 2)
        {
            _gate.TrySetResult();
        }

        using var timeoutCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await _gate.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"RaceCoordinator: only {Volatile.Read(ref _arrivals)} of 2 expected callers arrived within {_timeout}.");
        }
    }
}
