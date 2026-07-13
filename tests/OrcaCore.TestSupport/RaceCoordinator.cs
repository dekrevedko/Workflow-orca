namespace OrcaCore.TestSupport;

/// <summary>
/// Coordinates two async callers so both reach a gate before either continues.
/// </summary>
public sealed class RaceCoordinator
{
    private readonly TimeSpan timeout;
    private readonly TimeProvider timeProvider;
    private readonly TaskCompletionSource firstArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource bothArrived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivedCount;

    /// <summary>
    /// Initializes a race coordinator for exactly two callers.
    /// </summary>
    public RaceCoordinator(TimeSpan timeout, TimeProvider? timeProvider = null)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be positive.");
        }

        this.timeout = timeout;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the number of callers that reached the gate.
    /// </summary>
    public int ArrivedCount => Volatile.Read(ref arrivedCount);

    /// <summary>
    /// Marks the current caller as arrived and waits until both callers are present.
    /// </summary>
    public async Task ArriveAndWaitAsync(CancellationToken cancellationToken = default)
    {
        var count = Interlocked.Increment(ref arrivedCount);
        if (count > 2)
        {
            throw new InvalidOperationException("RaceCoordinator supports exactly two callers.");
        }

        if (count == 1)
        {
            firstArrived.TrySetResult();
        }

        if (count == 2)
        {
            bothArrived.TrySetResult();
        }

        await WaitWithTimeoutAsync(
            bothArrived.Task,
            "Timed out waiting for both race participants to arrive.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits until the expected number of callers have arrived.
    /// </summary>
    public async Task WaitForArrivalsAsync(int expectedArrivals, CancellationToken cancellationToken = default)
    {
        var waitTask = expectedArrivals switch
        {
            1 => firstArrived.Task,
            2 => bothArrived.Task,
            _ => throw new ArgumentOutOfRangeException(
                nameof(expectedArrivals),
                expectedArrivals,
                "Expected arrivals must be 1 or 2.")
        };

        await WaitWithTimeoutAsync(
            waitTask,
            $"Timed out waiting for {expectedArrivals} race participant(s) to arrive.",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitWithTimeoutAsync(
        Task task,
        string timeoutMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(timeout, timeProvider, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(timeoutMessage, exception);
        }
    }
}
