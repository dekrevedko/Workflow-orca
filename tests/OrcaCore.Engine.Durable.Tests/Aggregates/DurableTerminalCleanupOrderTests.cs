using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableTerminalCleanupOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParentCancelAndTerminate_EmitAllOwnedCleanupBeforeTerminal(bool terminate)
    {
        var instanceId = InstanceId.New();
        var fiberId = new FiberId("fiber:nested");
        var scopeId = new ScopeId("scope:nested");
        var waitId = WaitId.New();
        var timerId = TimerId.New();
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(instanceId),
                WaitRegistered(instanceId, fiberId, scopeId, waitId),
                TimerScheduled(instanceId, fiberId, scopeId, timerId),
                ExternalJobStarted(instanceId, fiberId, scopeId)
            ]);
        var commandId = CommandId.New();
        var requestedAt = Timestamp(5);

        var decision = terminate
            ? aggregate.DecideTerminate(new TerminateWorkflowCommand
            {
                CommandId = commandId,
                InstanceId = instanceId,
                RequestedAt = requestedAt
            })
            : aggregate.DecideCancel(new CancelWorkflowCommand
            {
                CommandId = commandId,
                InstanceId = instanceId,
                RequestedAt = requestedAt
            });

        var events = decision.Events.ToList();
        var terminalIndex = events.FindIndex(item => item is WorkflowTerminalEvent);
        var waitCancellation = events.OfType<WorkflowWaitCancelledEvent>()
            .Should().ContainSingle().Subject;
        var timerCancellation = events.OfType<WorkflowTimerCancelledEvent>()
            .Should().ContainSingle().Subject;
        var jobStop = events.OfType<WorkflowExternalJobStopRequestedEvent>()
            .Should().ContainSingle().Subject;

        waitCancellation.WaitId.Should().Be(waitId);
        timerCancellation.TimerId.Should().Be(timerId);
        waitCancellation.FiberId.Should().Be(fiberId);
        timerCancellation.ScopeId.Should().Be(scopeId);
        jobStop.FiberId.Should().Be(fiberId);
        events.IndexOf(waitCancellation).Should().BeLessThan(terminalIndex);
        events.IndexOf(timerCancellation).Should().BeLessThan(terminalIndex);
        events.IndexOf(jobStop).Should().BeLessThan(terminalIndex);
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegistered(
        InstanceId instanceId,
        FiberId fiberId,
        ScopeId scopeId,
        WaitId waitId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(2),
            WaitId = waitId,
            EventName = "resume",
            CorrelationId = new CorrelationId("terminal-cleanup"),
            FiberId = fiberId,
            ScopeId = scopeId,
            WaitSequence = 1
        };
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(
        InstanceId instanceId,
        FiberId fiberId,
        ScopeId scopeId,
        TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(3),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "delay",
            FiberId = fiberId,
            ScopeId = scopeId
        };
    }

    private static WorkflowExternalJobStartedEvent ExternalJobStarted(
        InstanceId instanceId,
        FiberId fiberId,
        ScopeId scopeId)
    {
        return new WorkflowExternalJobStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(4),
            ExternalJobId = "job:nested",
            Payload = [1],
            WaitId = WaitId.New(),
            FiberId = fiberId,
            ScopeId = scopeId
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 13, 13, 0, seconds, TimeSpan.Zero);
    }
}
