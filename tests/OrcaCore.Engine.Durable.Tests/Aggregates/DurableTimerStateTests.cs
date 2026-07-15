using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableTimerStateTests
{
    [Fact]
    public void ApplyScheduled_TracksActiveTimerForCheckpointProjection()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot([], []);

        state.Apply(TimerScheduled(timerId));

        state.FindActive(timerId)!.FiberId.Should().Be(new FiberId("timer-fiber"));
        state.FindActive(timerId)!.ScopeId.Should().Be(new ScopeId("timer-scope"));
        var checkpoint = state.CreateCheckpointActiveTimers().Should().ContainSingle().Which;
        checkpoint.FiberId.Should().Be(new FiberId("timer-fiber"));
        checkpoint.ScopeId.Should().Be(new ScopeId("timer-scope"));
        state.CreateCheckpointBufferedTimers().Should().BeEmpty();
    }

    [Fact]
    public void ApplyFired_RemovesActiveAndBufferedCopies()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot([], []);
        state.Apply(TimerScheduled(timerId));
        state.Apply(TimerBuffered(timerId));

        state.Apply(TimerFired(timerId));

        state.HasActiveTimers.Should().BeFalse();
        state.ActiveTimers.Should().BeEmpty();
        state.BufferedTimers.Should().BeEmpty();
    }

    [Fact]
    public void ApplyBuffered_ReplacesActiveTimerWithBufferedTimer()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot([], []);
        state.Apply(TimerScheduled(timerId));

        state.Apply(TimerBuffered(timerId));

        state.ActiveTimers.Should().BeEmpty();
        state.BufferedTimers.Should().ContainSingle().Which.Should().Be(
            new DurableBufferedTimer(timerId, "approval-timeout", Timestamp(5)));
        state.CreateCheckpointBufferedTimers().Should().ContainSingle().Which.Should().Be(
            new CheckpointBufferedTimer(timerId, "approval-timeout", Timestamp(5)));
    }

    [Fact]
    public void PlanBufferedReplay_WhenResumeReplaysBufferedTimers_EmitsTimerFiredEvents()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot([], [new DurableBufferedTimer(timerId, "approval-timeout", Timestamp(5))]);

        var events = state.PlanBufferedReplay(
            new DurableTimerEventContext(CommandIdValue(9), InstanceIdValue(1), Timestamp(9)),
            ResumeBufferedDeliveries.Replay);

        events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowTimerFiredEvent>()
            .Which.Should().Match<WorkflowTimerFiredEvent>(workflowEvent =>
                workflowEvent.TimerId == timerId &&
                workflowEvent.CommandId == CommandIdValue(9) &&
                workflowEvent.InstanceId == InstanceIdValue(1) &&
                workflowEvent.OccurredAt == Timestamp(9));
    }

    [Fact]
    public void PlanBufferedReplay_WhenResumeDiscardsBufferedTimers_EmitsNoEvents()
    {
        var state = DurableTimerState.FromSnapshot(
            [],
            [new DurableBufferedTimer(TimerIdValue(1), "approval-timeout", Timestamp(5))]);

        var events = state.PlanBufferedReplay(
            new DurableTimerEventContext(CommandIdValue(9), InstanceIdValue(1), Timestamp(9)),
            ResumeBufferedDeliveries.Discard);

        events.Should().BeEmpty();
    }

    [Fact]
    public void Clear_RemovesActiveAndBufferedTimers()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot(
            [new DurableActiveTimer(timerId, Timestamp(30), "approval-timeout", Timestamp(1))],
            [new DurableBufferedTimer(timerId, "approval-timeout", Timestamp(5))]);

        state.Clear();

        state.ActiveTimers.Should().BeEmpty();
        state.BufferedTimers.Should().BeEmpty();
    }

    [Fact]
    public void SnapshotViews_DoNotExposeMutableTimerCollections()
    {
        var timerId = TimerIdValue(1);
        var state = DurableTimerState.FromSnapshot([], []);
        var activeSnapshot = state.ActiveTimers;
        var bufferedSnapshot = state.BufferedTimers;

        state.Apply(TimerScheduled(timerId));
        state.Apply(TimerBuffered(timerId));

        activeSnapshot.Should().BeEmpty();
        bufferedSnapshot.Should().BeEmpty();
        state.ActiveTimers.Should().BeEmpty();
        state.BufferedTimers.Should().ContainSingle();
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout",
            FiberId = new FiberId("timer-fiber"),
            ScopeId = new ScopeId("timer-scope")
        };
    }

    private static WorkflowTimerBufferedEvent TimerBuffered(TimerId timerId)
    {
        return new WorkflowTimerBufferedEvent
        {
            EventId = EventIdValue(5),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(5),
            CausationId = CausationIdValue(5),
            OccurredAt = Timestamp(5),
            TimerId = timerId,
            WakeupName = "approval-timeout"
        };
    }

    private static WorkflowTimerFiredEvent TimerFired(TimerId timerId)
    {
        return new WorkflowTimerFiredEvent
        {
            EventId = EventIdValue(9),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(9),
            CausationId = CausationIdValue(9),
            OccurredAt = Timestamp(9),
            TimerId = timerId
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 21, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
