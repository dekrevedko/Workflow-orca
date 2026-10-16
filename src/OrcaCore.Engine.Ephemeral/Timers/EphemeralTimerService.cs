using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral.Timers;

internal sealed class EphemeralTimerService(TimeProvider timeProvider) : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private readonly List<ScheduledTimer> scheduledTimers = [];
    private readonly HashSet<Guid> claimedTimerTokens = [];
    private readonly object gate = new();
    private Func<CancellationToken, Task>? dispatchDueAsync;
    private int dispatching;
    private int disposed;

    internal bool IsDispatching => Volatile.Read(ref dispatching) != 0;

    internal void SetDueDispatcher(Func<CancellationToken, Task> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (Interlocked.CompareExchange(ref dispatchDueAsync, dispatcher, null) is not null)
        {
            throw new InvalidOperationException("The ephemeral timer dispatcher is already configured.");
        }
    }

    internal ScheduledTimer Schedule(
        InstanceId instanceId,
        TimeSpan delay,
        Func<CancellationToken, Task<EphemeralWorkflowInstanceSnapshot>> fireAsync)
    {
        ArgumentNullException.ThrowIfNull(fireAsync);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        var scheduledTimer = new ScheduledTimer(
            Guid.CreateVersion7(),
            instanceId,
            timeProvider.GetUtcNow().Add(delay),
            fireAsync);
        scheduledTimer.AttachClockTimer(timeProvider.CreateTimer(
            static state => ((EphemeralTimerService)state!).SignalDue(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan));
        lock (gate)
        {
            scheduledTimers.Add(scheduledTimer);
        }

        scheduledTimer.Rearm(delay > TimeSpan.Zero ? delay : TimeSpan.Zero);

        return scheduledTimer;
    }

    internal void Cancel(ScheduledTimer scheduledTimer)
    {
        lock (gate)
        {
            scheduledTimers.RemoveAll(timer => timer.Token == scheduledTimer.Token);
            claimedTimerTokens.Remove(scheduledTimer.Token);
        }

        scheduledTimer.Dispose();
    }

    internal void Complete(ScheduledTimer scheduledTimer)
    {
        lock (gate)
        {
            scheduledTimers.RemoveAll(timer => timer.Token == scheduledTimer.Token);
            claimedTimerTokens.Remove(scheduledTimer.Token);
        }

        scheduledTimer.Dispose();
    }

    internal void Release(ScheduledTimer scheduledTimer)
    {
        lock (gate)
        {
            claimedTimerTokens.Remove(scheduledTimer.Token);
        }
    }

    internal IReadOnlyList<ScheduledTimer> ClaimDueTimers()
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            var due = scheduledTimers
                .Where(timer => timer.FireAt <= now && !claimedTimerTokens.Contains(timer.Token))
                .OrderBy(timer => timer.FireAt)
                .ToArray();

            if (due.Length == 0)
            {
                return [];
            }

            foreach (var timer in due)
            {
                claimedTimerTokens.Add(timer.Token);
            }

            return due;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        ScheduledTimer[] timers;
        lock (gate)
        {
            timers = [.. scheduledTimers];
            scheduledTimers.Clear();
            claimedTimerTokens.Clear();
        }

        foreach (var timer in timers)
        {
            timer.Dispose();
        }
    }

    private void SignalDue()
    {
        if (Volatile.Read(ref disposed) != 0 ||
            dispatchDueAsync is null ||
            Interlocked.CompareExchange(ref dispatching, 1, 0) != 0)
        {
            return;
        }

        _ = DispatchDueSafelyAsync();
    }

    private async Task DispatchDueSafelyAsync()
    {
        var failed = false;
        try
        {
            await dispatchDueAsync!(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            failed = true;
            RearmReleasedDueTimers();
        }
        finally
        {
            Volatile.Write(ref dispatching, 0);
            if (!failed && HasUnclaimedDueTimers())
            {
                SignalDue();
            }
        }
    }

    private bool HasUnclaimedDueTimers()
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            return scheduledTimers.Any(timer =>
                timer.FireAt <= now && !claimedTimerTokens.Contains(timer.Token));
        }
    }

    private void RearmReleasedDueTimers()
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            foreach (var timer in scheduledTimers.Where(timer =>
                         timer.FireAt <= now && !claimedTimerTokens.Contains(timer.Token)))
            {
                timer.Rearm(RetryDelay);
            }
        }
    }
}

internal sealed class ScheduledTimer(
    Guid token,
    InstanceId instanceId,
    DateTimeOffset fireAt,
    Func<CancellationToken, Task<EphemeralWorkflowInstanceSnapshot>> fireAsync) : IDisposable
{
    private ITimer? clockTimer;

    internal Guid Token { get; } = token;

    internal InstanceId InstanceId { get; } = instanceId;

    internal DateTimeOffset FireAt { get; } = fireAt;

    internal Func<CancellationToken, Task<EphemeralWorkflowInstanceSnapshot>> FireAsync { get; } = fireAsync;

    internal void AttachClockTimer(ITimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        if (Interlocked.CompareExchange(ref clockTimer, timer, null) is not null)
        {
            timer.Dispose();
            throw new InvalidOperationException("The scheduled timer is already armed.");
        }
    }

    internal void Rearm(TimeSpan dueTime) =>
        Volatile.Read(ref clockTimer)?.Change(dueTime, Timeout.InfiniteTimeSpan);

    public void Dispose() => Interlocked.Exchange(ref clockTimer, null)?.Dispose();
}
