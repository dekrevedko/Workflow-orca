using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableTimerState
{
    private readonly List<DurableActiveTimer> activeTimers;

    private DurableTimerState(IEnumerable<DurableActiveTimer> activeTimers)
    {
        this.activeTimers = [.. activeTimers];
    }

    internal IReadOnlyList<DurableActiveTimer> ActiveTimers => [.. activeTimers];

    internal bool HasActiveTimers => activeTimers.Count > 0;

    internal static DurableTimerState FromSnapshot(IEnumerable<DurableActiveTimer> activeTimers)
    {
        ArgumentNullException.ThrowIfNull(activeTimers);

        return new DurableTimerState(activeTimers);
    }

    internal DurableActiveTimer? FindActive(TimerId timerId)
    {
        return activeTimers.FirstOrDefault(timer => timer.TimerId == timerId);
    }

    internal void Apply(WorkflowTimerScheduledEvent timerScheduled)
    {
        ArgumentNullException.ThrowIfNull(timerScheduled);

        activeTimers.Add(new DurableActiveTimer(
            timerScheduled.TimerId,
            timerScheduled.FireAt,
            timerScheduled.WakeupName,
            timerScheduled.OccurredAt)
        {
            FiberId = timerScheduled.FiberId,
            ScopeId = timerScheduled.ScopeId
        });
    }

    internal void Apply(WorkflowTimerFiredEvent timerFired)
    {
        ArgumentNullException.ThrowIfNull(timerFired);

        activeTimers.RemoveAll(timer => timer.TimerId == timerFired.TimerId);
    }

    internal void Apply(WorkflowTimerCancelledEvent timerCancelled)
    {
        ArgumentNullException.ThrowIfNull(timerCancelled);

        activeTimers.RemoveAll(timer => timer.TimerId == timerCancelled.TimerId);
    }

    internal IReadOnlyList<CheckpointActiveTimer> CreateCheckpointActiveTimers()
    {
        return activeTimers
            .Select(timer => new CheckpointActiveTimer(
                timer.TimerId,
                timer.FireAt,
                timer.WakeupName,
                timer.RegisteredAt)
            {
                FiberId = timer.FiberId,
                ScopeId = timer.ScopeId
            })
            .ToArray();
    }

    internal void Clear()
    {
        activeTimers.Clear();
    }
}
