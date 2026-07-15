using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableCommitMaterializerTests
{
    [Fact]
    public void CreateBatch_WhenStartHasIdempotencyKey_IncludesStartIdempotencyWrite()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, []);
        var decision = new DurableDecision([Started(instanceId, "start-key")]);
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(instanceId, StreamVersion.Empty, decision, aggregate, inboxEventId: null);

        batch.StartIdempotencyOperations.Should().ContainSingle().Which.Should().Be(
            new StartIdempotencyWrite(
                "start-key",
                instanceId,
                DefinitionIdValue(1),
                DefinitionVersion.Initial));
    }

    [Fact]
    public void CreateBatch_WhenTimerScheduled_IncludesTimerSchedule()
    {
        var instanceId = InstanceIdValue(1);
        var timerId = TimerIdValue(10);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var decision = new DurableDecision([TimerScheduled(instanceId, timerId)]);
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(instanceId, new StreamVersion(1), decision, aggregate, inboxEventId: null);

        batch.TimerSchedules.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TimerScheduleRequest
            {
                TimerId = timerId,
                InstanceId = instanceId,
                CommandId = new CommandId(EventIdValue(10).Value),
                FireAt = Timestamp(30),
                WakeupName = "approval-timeout"
            });
    }

    [Fact]
    public void CreateBatch_WhenInboundDeliveryIsBuffered_KeepsInboundReceivedAndDecisionInboxWrites()
    {
        var instanceId = InstanceIdValue(1);
        var inboxEventId = EventIdValue(99);
        var bufferedEventId = EventIdValue(100);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var decision = new DurableDecision(
            [DeliveryBuffered(instanceId, bufferedEventId)],
            inboxOperations: [new InboxWrite(bufferedEventId, InboxRecordState.Received)]);
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(instanceId, new StreamVersion(1), decision, aggregate, inboxEventId);

        batch.InboxOperations.Should().Equal(
            new InboxWrite(inboxEventId, InboxRecordState.Received),
            new InboxWrite(bufferedEventId, InboxRecordState.Received));
    }

    [Fact]
    public void CreateBatch_MaterializesOutboxKindsFromDurableEvents()
    {
        var instanceId = InstanceIdValue(1);
        var childId = InstanceIdValue(2);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var decision = new DurableDecision(
            [
                StepCompleted(instanceId),
                ChildScheduled(instanceId, childId),
                ResidualIntent(instanceId, childId),
                ExternalJobStarted(instanceId),
                ExternalJobStopRequested(instanceId)
            ]);
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(instanceId, new StreamVersion(1), decision, aggregate, inboxEventId: null);

        batch.OutboxRecords.Select(record => record.Kind).Should().BeEquivalentTo(
            "lifecycle-event",
            "child-start",
            "child-cancel",
            "external-job-start",
            "external-job-stop");
    }

    [Fact]
    public void CreateBatch_WhenFormat2CheckpointHasRunnableSibling_EmitsContinuation()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var envelope = RunnableEnvelope(instanceId);
        var decision = new DurableDecision(
            [WaitRegistered(instanceId)],
            new CheckpointWrite(
                instanceId,
                new StreamVersion(2),
                DurableExecutionEnvelopeV2.ContentType,
                envelope.Serialize()));
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(
            instanceId,
            new StreamVersion(1),
            decision,
            aggregate,
            inboxEventId: null);

        batch.OutboxRecords.Should().ContainSingle(record => record.Kind == OutboxKinds.Continue);
    }

    [Fact]
    public void CreateBatch_WhenCheckpointOnlyDecisionLeavesRunnableFiber_EmitsContinuation()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var decision = new DurableDecision(
            [],
            new CheckpointWrite(
                instanceId,
                new StreamVersion(1),
                DurableExecutionEnvelopeV2.ContentType,
                RunnableEnvelope(instanceId).Serialize()));
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateBatch(
            instanceId,
            new StreamVersion(1),
            decision,
            aggregate,
            inboxEventId: null);

        batch.OutboxRecords.Should().ContainSingle(record => record.Kind == OutboxKinds.Continue);
    }

    [Fact]
    public void CreateInboxOnlyBatch_WritesOnlyInboxOperation()
    {
        var instanceId = InstanceIdValue(1);
        var inboxEventId = EventIdValue(99);
        var materializer = new DurableCommitMaterializer();

        var batch = materializer.CreateInboxOnlyBatch(
            instanceId,
            new StreamVersion(3),
            inboxEventId,
            InboxRecordState.Poisoned);

        batch.StreamId.Should().Be(new WorkflowStreamId(instanceId));
        batch.ExpectedVersion.Should().Be(new StreamVersion(3));
        batch.Events.Should().BeEmpty();
        batch.OutboxRecords.Should().BeEmpty();
        batch.InboxOperations.Should().ContainSingle().Which.Should().Be(
            new InboxWrite(inboxEventId, InboxRecordState.Poisoned));
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId, string? idempotencyKey = null)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial,
            IdempotencyKey = idempotencyKey
        };
    }

    private static DurableExecutionEnvelopeV2 RunnableEnvelope(InstanceId instanceId)
    {
        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = instanceId,
            ContinueAsNewGeneration = 0,
            RootFiberId = "root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = DefinitionIdValue(1),
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 1,
                PlanFingerprint = "fingerprint"
            },
            StateContentType = "application/json",
            StatePayload = "{}"u8.ToArray(),
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "root",
                    InstructionId = "step:next",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 1
                }
            ],
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["root"],
                NextFiberId = "root"
            }
        };
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(InstanceId instanceId, TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventIdValue(10),
            InstanceId = instanceId,
            CommandId = CommandIdValue(10),
            CausationId = CausationIdValue(10),
            OccurredAt = Timestamp(10),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout"
        };
    }

    private static WorkflowStepCompletedEvent StepCompleted(InstanceId instanceId)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(11),
            InstanceId = instanceId,
            CommandId = CommandIdValue(11),
            CausationId = CausationIdValue(11),
            OccurredAt = Timestamp(11),
            StepPath = "root/1"
        };
    }

    private static WorkflowChildScheduledEvent ChildScheduled(InstanceId instanceId, InstanceId childId)
    {
        return new WorkflowChildScheduledEvent
        {
            EventId = EventIdValue(12),
            InstanceId = instanceId,
            CommandId = CommandIdValue(12),
            CausationId = CausationIdValue(12),
            OccurredAt = Timestamp(12),
            ChildInstanceId = childId,
            ChildDefinitionId = DefinitionIdValue(2),
            ChildDefinitionVersion = DefinitionVersion.Initial,
            WaitId = WaitIdValue(12),
            FailurePolicy = RunChildFailurePolicy.PropagateFailure
        };
    }

    private static WorkflowChildResidualIntentRecordedEvent ResidualIntent(InstanceId instanceId, InstanceId childId)
    {
        return new WorkflowChildResidualIntentRecordedEvent
        {
            EventId = EventIdValue(13),
            InstanceId = instanceId,
            CommandId = CommandIdValue(13),
            CausationId = CausationIdValue(13),
            OccurredAt = Timestamp(13),
            GroupId = "group-1",
            ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
            ResidualChildInstanceIds = [childId]
        };
    }

    private static WorkflowExternalJobStartedEvent ExternalJobStarted(InstanceId instanceId)
    {
        return new WorkflowExternalJobStartedEvent
        {
            EventId = EventIdValue(14),
            InstanceId = instanceId,
            CommandId = CommandIdValue(14),
            CausationId = CausationIdValue(14),
            OccurredAt = Timestamp(14),
            ExternalJobId = "job-1",
            Payload = [1, 2, 3],
            WaitId = WaitIdValue(14)
        };
    }

    private static WorkflowExternalJobStopRequestedEvent ExternalJobStopRequested(InstanceId instanceId)
    {
        return new WorkflowExternalJobStopRequestedEvent
        {
            EventId = EventIdValue(15),
            InstanceId = instanceId,
            CommandId = CommandIdValue(15),
            CausationId = CausationIdValue(15),
            OccurredAt = Timestamp(15),
            ExternalJobId = "job-1"
        };
    }

    private static WorkflowDeliveryBufferedEvent DeliveryBuffered(InstanceId instanceId, EventId bufferedEventId)
    {
        return new WorkflowDeliveryBufferedEvent
        {
            EventId = EventIdValue(20),
            InstanceId = instanceId,
            CommandId = CommandIdValue(20),
            CausationId = CausationIdValue(20),
            OccurredAt = Timestamp(20),
            BufferedEventId = bufferedEventId,
            EventName = "Approved",
            CorrelationId = new CorrelationId("order-1")
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegistered(InstanceId instanceId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventIdValue(21),
            InstanceId = instanceId,
            CommandId = CommandIdValue(21),
            CausationId = CausationIdValue(21),
            OccurredAt = Timestamp(21),
            WaitId = WaitIdValue(21),
            EventName = "Continue",
            CorrelationId = new CorrelationId("order-1")
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 18, 0, seconds, TimeSpan.Zero);
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

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
