using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableTimerAggregateTests
{
    [Fact]
    public void ScheduleTimer_RecordsTimerScheduledEvent()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var command = new ScheduleTimerCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(2),
            TimerId = TimerIdValue(10),
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout",
            FiberId = new FiberId("timer-fiber"),
            ScopeId = new ScopeId("timer-scope")
        };

        var decision = aggregate.DecideTimerScheduled(command);

        var scheduled = decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowTimerScheduledEvent>().Subject;
        scheduled.TimerId.Should().Be(command.TimerId);
        scheduled.FireAt.Should().Be(command.FireAt);
        scheduled.WakeupName.Should().Be(command.WakeupName);
        scheduled.FiberId.Should().Be(command.FiberId);
        scheduled.ScopeId.Should().Be(command.ScopeId);
        var replayed = DurableWorkflowAggregate.Rehydrate(null, [Started(), .. decision.Events]);
        replayed.Snapshot.ActiveTimers.Single().FiberId.Should().Be(command.FiberId);
        replayed.Snapshot.ActiveTimers.Single().ScopeId.Should().Be(command.ScopeId);
    }

    [Fact]
    public void FireTimer_ForActiveTimer_RecordsTimerFiredEvent()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(TimerIdValue(10))]);
        var command = new FireTimerCommand
        {
            CommandId = CommandIdValue(3),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(30),
            TimerId = TimerIdValue(10)
        };

        var decision = aggregate.DecideTimerFired(command);

        decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowTimerFiredEvent>()
            .Which.TimerId.Should().Be(command.TimerId);
    }

    [Fact]
    public void FireTimer_ForAlreadyFiredTimer_IsNoOp()
    {
        var timerId = TimerIdValue(10);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(timerId), TimerFired(timerId)]);
        var command = new FireTimerCommand
        {
            CommandId = CommandIdValue(4),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(31),
            TimerId = timerId
        };

        var decision = aggregate.DecideTimerFired(command);

        decision.Events.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-513")]
    public void FireTimer_WhenPaused_BuffersWithoutAdvancing()
    {
        var timerId = TimerIdValue(10);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(timerId), Paused()]);
        var command = new FireTimerCommand
        {
            CommandId = CommandIdValue(4),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(30),
            TimerId = timerId
        };

        var decision = aggregate.DecideTimerFired(command);
        var afterBuffer = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(timerId), Paused(), .. decision.Events]);

        decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowTimerBufferedEvent>()
            .Which.TimerId.Should().Be(timerId);
        decision.Events.Should().NotContain(workflowEvent => workflowEvent is WorkflowTimerFiredEvent);
        afterBuffer.Snapshot.Status.Should().Be(WorkflowStatus.Paused);
    }

    private static WorkflowStartedEvent Started()
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = new DefinitionVersion(7)
        };
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout"
        };
    }

    private static WorkflowTimerFiredEvent TimerFired(TimerId timerId)
    {
        return new WorkflowTimerFiredEvent
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(30),
            TimerId = timerId
        };
    }

    private static WorkflowPausedEvent Paused()
    {
        return new WorkflowPausedEvent
        {
            EventId = EventIdValue(4),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4)
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
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

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
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
