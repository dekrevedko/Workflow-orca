using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Timers;

internal sealed class EphemeralTimerService(TimeProvider timeProvider)
{
    private readonly List<ScheduledTimer> scheduledTimers = [];
    private readonly object gate = new();

    internal ScheduledTimer Schedule(
        InstanceId instanceId,
        TimeSpan delay,
        Func<CancellationToken, Task<WorkflowInstanceSnapshot>> fireAsync)
    {
        ArgumentNullException.ThrowIfNull(fireAsync);

        var scheduledTimer = new ScheduledTimer(
            Guid.NewGuid(),
            instanceId,
            timeProvider.GetUtcNow().Add(delay),
            fireAsync);
        lock (gate)
        {
            scheduledTimers.Add(scheduledTimer);
        }

        return scheduledTimer;
    }

    internal void Cancel(ScheduledTimer scheduledTimer)
    {
        lock (gate)
        {
            scheduledTimers.RemoveAll(timer => timer.Token == scheduledTimer.Token);
        }
    }

    internal IReadOnlyList<ScheduledTimer> ClaimDueTimers()
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            var due = scheduledTimers
                .Where(timer => timer.FireAt <= now)
                .OrderBy(timer => timer.FireAt)
                .ToArray();

            if (due.Length == 0)
            {
                return [];
            }

            scheduledTimers.RemoveAll(timer => due.Contains(timer));
            return due;
        }
    }
}

internal sealed record ScheduledTimer(
    Guid Token,
    InstanceId InstanceId,
    DateTimeOffset FireAt,
    Func<CancellationToken, Task<WorkflowInstanceSnapshot>> FireAsync);
