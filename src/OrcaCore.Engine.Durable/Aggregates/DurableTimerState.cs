using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableTimerState
{
    private readonly List<DurableActiveTimer> activeTimers;
    private readonly List<DurableBufferedTimer> bufferedTimers;

    private DurableTimerState(
        IEnumerable<DurableActiveTimer> activeTimers,
        IEnumerable<DurableBufferedTimer> bufferedTimers)
    {
        this.activeTimers = [.. activeTimers];
        this.bufferedTimers = [.. bufferedTimers];
    }

    internal IReadOnlyList<DurableActiveTimer> ActiveTimers => [.. activeTimers];

    internal IReadOnlyList<DurableBufferedTimer> BufferedTimers => [.. bufferedTimers];

    internal bool HasActiveTimers => activeTimers.Count > 0;

    internal static DurableTimerState FromSnapshot(
        IEnumerable<DurableActiveTimer> activeTimers,
        IEnumerable<DurableBufferedTimer> bufferedTimers)
    {
        ArgumentNullException.ThrowIfNull(activeTimers);
        ArgumentNullException.ThrowIfNull(bufferedTimers);

        return new DurableTimerState(activeTimers, bufferedTimers);
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
        bufferedTimers.RemoveAll(timer => timer.TimerId == timerFired.TimerId);
    }

    internal void Apply(WorkflowTimerCancelledEvent timerCancelled)
    {
        ArgumentNullException.ThrowIfNull(timerCancelled);

        activeTimers.RemoveAll(timer => timer.TimerId == timerCancelled.TimerId);
        bufferedTimers.RemoveAll(timer => timer.TimerId == timerCancelled.TimerId);
    }

    internal void Apply(WorkflowTimerBufferedEvent timerBuffered)
    {
        ArgumentNullException.ThrowIfNull(timerBuffered);

        activeTimers.RemoveAll(timer => timer.TimerId == timerBuffered.TimerId);
        bufferedTimers.RemoveAll(timer => timer.TimerId == timerBuffered.TimerId);
        bufferedTimers.Add(new DurableBufferedTimer(
            timerBuffered.TimerId,
            timerBuffered.WakeupName,
            timerBuffered.OccurredAt));
    }

    internal IReadOnlyList<WorkflowEvent> PlanBufferedReplay(
        DurableTimerEventContext context,
        ResumeBufferedDeliveries handling)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (handling == ResumeBufferedDeliveries.Discard)
        {
            return [];
        }

        return bufferedTimers
            .Select(bufferedTimer => new WorkflowTimerFiredEvent
            {
                EventId = EventId.New(),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = new CausationId(context.CommandId.Value),
                OccurredAt = context.RequestedAt,
                TimerId = bufferedTimer.TimerId
            })
            .ToArray();
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

    internal IReadOnlyList<CheckpointBufferedTimer> CreateCheckpointBufferedTimers()
    {
        return bufferedTimers
            .Select(timer => new CheckpointBufferedTimer(
                timer.TimerId,
                timer.WakeupName,
                timer.BufferedAt))
            .ToArray();
    }

    internal void Clear()
    {
        activeTimers.Clear();
        bufferedTimers.Clear();
    }
}

internal sealed record DurableTimerEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt);
