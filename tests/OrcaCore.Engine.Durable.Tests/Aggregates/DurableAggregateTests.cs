using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableAggregateTests
{
    [Fact]
    public void Rehydrate_CheckpointPlusTail_RestoresSameStateAsFullReplay()
    {
        var events = new DurableWorkflowEvent[]
        {
            Started(),
            StepCompleted("root/1"),
            WaitRegistered("Approved", CorrelationId.Create("order-1"))
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
    public void DecideStart_RejectsPayloadOutsideTheFixedWorkflowCodec()
    {
        var command = new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            InputContentType = "application/x-attacker-protobuf",
            InputPayload = [0xDE, 0xAD, 0xBE, 0xEF]
        };
        var aggregate = DurableWorkflowAggregate.Empty(command.InstanceId);

        var act = () => aggregate.DecideStart(command);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(command.InputContentType));
    }

    [Fact]
    public void DecideStepCompleted_EmitsStepAndStateCheckpointFacts()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var envelope = TestEnvelopes.Envelope("application/octet-stream", [9, 8, 7]);
        var command = new DurableStepCompletedCommand(
            CommandIdValue(2),
            InstanceIdValue(1),
            Timestamp(2),
            "root/1",
            envelope);

        var decision = aggregate.DecideStepCompleted(command);

        decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStepCompletedEvent>()
            .Which.StepPath.Should().Be("root/1");
        decision.Checkpoint.Should().NotBeNull();
        decision.Checkpoint!.StreamVersion.Should().Be(new StreamVersion(2));
        decision.Checkpoint.ContentType.Should().Be(DurableExecutionEnvelopeV2.ContentType);
        var persisted = DurableExecutionEnvelopeV2.Deserialize(decision.Checkpoint.Payload);
        persisted.StateContentType.Should().Be("application/octet-stream");
        persisted.StatePayload.Should().Equal(9, 8, 7);
        persisted.Fibers.Should().ContainSingle().Which.FiberId.Should().Be("root");
    }

    [Fact]
    public void DecideStepCompleted_CommitsFormat2FiberEnvelopeAtomically()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var envelope = new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = aggregate.InstanceId,
            ContinueAsNewGeneration = 0,
            RootFiberId = "root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = DefinitionIdValue(1),
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 1,
                CompilerProfileId = "orcacore-compiler-v1;quantum=1024",
                PlanFingerprint = "fingerprint"
            },
            StateContentType = "application/json",
            StatePayload = [7],
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "root",
                    InstructionId = "root/2",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0
                }
            ],
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["root"],
                NextFiberId = "root"
            }
        };
        var command = new DurableStepCompletedCommand(
            CommandIdValue(2),
            aggregate.InstanceId,
            Timestamp(2),
            "root/1",
            envelope);

        var decision = aggregate.DecideStepCompleted(command);

        decision.Events.Should().ContainSingle().Which.Should().BeOfType<WorkflowStepCompletedEvent>();
        decision.Checkpoint.Should().NotBeNull();
        decision.Checkpoint!.ContentType.Should().Be(DurableExecutionEnvelopeV2.ContentType);
        var persisted = DurableExecutionEnvelopeV2.Deserialize(decision.Checkpoint.Payload);
        persisted.PlanBinding.PlanFingerprint.Should().Be("fingerprint");
        persisted.Fibers.Single().InstructionId.Should().Be("root/2");
    }

    [Fact]
    [Trait("AC", "AC-519")]
    public void DecideYield_WritesCheckpointWithoutCompletingStep()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started()]);
        var command = new DurableYieldCommand(
            CommandIdValue(2),
            InstanceIdValue(1),
            Timestamp(2),
            "root/1",
            TestEnvelopes.Envelope("application/json", [1, 2, 3]));

        var decision = aggregate.DecideYield(command);

        decision.Events.Should().BeEmpty();
        decision.Checkpoint.Should().NotBeNull();
        decision.Checkpoint!.StreamVersion.Should().Be(new StreamVersion(1));
        decision.Checkpoint.LastStepPath.Should().Be("root/1");
        decision.Checkpoint.ContentType.Should().Be(DurableExecutionEnvelopeV2.ContentType);
        var persisted = DurableExecutionEnvelopeV2.Deserialize(decision.Checkpoint.Payload);
        persisted.StateContentType.Should().Be("application/json");
        persisted.StatePayload.Should().Equal(1, 2, 3);
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
            TestEnvelopes.Envelope())).Events.Should().BeEmpty();
    }

    [Fact]
    public void Replay_ResumeWithOnlyActiveTimerPending_ReportsWaiting()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                new WorkflowTimerScheduledEvent
                {
                    EventId = EventIdValue(5),
                    InstanceId = InstanceIdValue(1),
                    CommandId = CommandIdValue(5),
                    CausationId = CausationIdValue(5),
                    OccurredAt = Timestamp(5),
                    TimerId = new TimerId(GuidValue(50)),
                    FireAt = Timestamp(40),
                    WakeupName = "timeout"
                },
                new WorkflowPausedEvent
                {
                    EventId = EventIdValue(6),
                    InstanceId = InstanceIdValue(1),
                    CommandId = CommandIdValue(6),
                    CausationId = CausationIdValue(6),
                    OccurredAt = Timestamp(6)
                },
                new WorkflowResumedEvent
                {
                    EventId = EventIdValue(7),
                    InstanceId = InstanceIdValue(1),
                    CommandId = CommandIdValue(7),
                    CausationId = CausationIdValue(7),
                    OccurredAt = Timestamp(7),
                    BufferHandling = "replay"
                }
            ]);

        aggregate.Status.Should().Be(
            WorkflowStatus.Waiting,
            "an instance resumed while a timer is still pending is waiting on that timer, not runnable");
    }

    [Fact]
    public void Replay_SameEventsTwice_IsDeterministic()
    {
        var events = new DurableWorkflowEvent[]
        {
            Started(),
            StepCompleted("root/1"),
            WaitRegistered("Approved", CorrelationId.Create("order-1")),
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
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
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
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
