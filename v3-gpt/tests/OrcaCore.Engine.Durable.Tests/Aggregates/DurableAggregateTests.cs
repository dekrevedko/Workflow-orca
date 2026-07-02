using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableAggregateTests
{
    [Fact]
    public void Rehydrate_CheckpointPlusTail_RestoresSameStateAsFullReplay()
    {
        var events = new WorkflowEvent[]
        {
            Started(),
            StepCompleted("root/1"),
            WaitRegistered("Approved", new CorrelationId("order-1"))
        };
        var prefix = DurableWorkflowAggregate.Rehydrate(null, events.Take(2));
        var checkpoint = prefix.CreateCheckpoint("application/octet-stream", [1, 2, 3]);

        var fullReplay = DurableWorkflowAggregate.Rehydrate(null, events);
        var checkpointReplay = DurableWorkflowAggregate.Rehydrate(checkpoint, events.Skip(2));

        checkpointReplay.Snapshot.Should().BeEquivalentTo(fullReplay.Snapshot);
        checkpointReplay.StreamVersion.Should().Be(fullReplay.StreamVersion);
    }

    [Fact]
    public void DecideStart_EmitsStartedAndVersionBoundEvents()
    {
        var command = new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = new DefinitionVersion(7)
        };
        var aggregate = DurableWorkflowAggregate.Empty(command.InstanceId);

        var decision = aggregate.DecideStart(command);

        var started = decision.Events.Should().ContainSingle().Which.Should().BeOfType<WorkflowStartedEvent>().Subject;
        started.DefinitionId.Should().Be(command.DefinitionId);
        started.DefinitionVersion.Should().Be(command.DefinitionVersion);
        started.CommandId.Should().Be(command.CommandId);
        started.InstanceId.Should().Be(command.InstanceId);
    }

    [Fact]
    public void DecideStepCompleted_EmitsStepAndStateCheckpointFacts()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var command = new DurableStepCompletedCommand(
            CommandIdValue(2),
            InstanceIdValue(1),
            Timestamp(2),
            "root/1",
            "application/octet-stream",
            [9, 8, 7]);

        var decision = aggregate.DecideStepCompleted(command);

        decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStepCompletedEvent>()
            .Which.StepPath.Should().Be("root/1");
        decision.Checkpoint.Should().NotBeNull();
        decision.Checkpoint!.StreamVersion.Should().Be(new StreamVersion(2));
        decision.Checkpoint.ContentType.Should().Be("application/octet-stream");
        decision.Checkpoint.Payload.Should().Equal(9, 8, 7);
    }

    [Fact]
    public void DecideStepFailed_EmitsFailedAndStopsFurtherDecisions()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var failed = aggregate.DecideStepFailed(new DurableStepFailedCommand(
            CommandIdValue(2),
            InstanceIdValue(1),
            Timestamp(2),
            "root/1",
            "boom"));
        var terminal = DurableWorkflowAggregate.Rehydrate(null, [Started(), .. failed.Events]);

        failed.Events.Should().HaveCount(2);
        failed.Events.OfType<WorkflowStepFailedEvent>().Single().ErrorSummary.Should().Be("boom");
        failed.Events.OfType<WorkflowTerminalEvent>().Single().Status.Should().Be(WorkflowStatus.Failed);
        terminal.DecideStepCompleted(new DurableStepCompletedCommand(
            CommandIdValue(3),
            InstanceIdValue(1),
            Timestamp(3),
            "root/2",
            "application/octet-stream",
            [1])).Events.Should().BeEmpty();
    }

    [Fact]
    public void Replay_SameEventsTwice_IsDeterministic()
    {
        var events = new WorkflowEvent[]
        {
            Started(),
            StepCompleted("root/1"),
            WaitRegistered("Approved", new CorrelationId("order-1")),
            WaitMatched()
        };

        var first = DurableWorkflowAggregate.Rehydrate(null, events);
        var second = DurableWorkflowAggregate.Rehydrate(null, events);

        second.Snapshot.Should().BeEquivalentTo(first.Snapshot);
        second.StreamVersion.Should().Be(first.StreamVersion);
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

    private static WorkflowStepCompletedEvent StepCompleted(string stepPath)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = stepPath
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegistered(string eventName, CorrelationId correlationId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            WaitId = WaitIdValue(1),
            EventName = eventName,
            CorrelationId = correlationId
        };
    }

    private static WorkflowWaitMatchedEvent WaitMatched()
    {
        return new WorkflowWaitMatchedEvent
        {
            EventId = EventIdValue(4),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4),
            WaitId = WaitIdValue(1),
            MatchedEventId = EventIdValue(30)
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

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
