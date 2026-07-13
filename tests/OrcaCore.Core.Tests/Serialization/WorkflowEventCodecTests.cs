using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using Xunit;

namespace OrcaCore.Core.Tests.Serialization;

public sealed class WorkflowEventCodecTests
{
    [Fact]
    public void EventTypeNames_AreFrozenStreamDiscriminators()
    {
        // These names are persisted in durable event streams. An entry may never change or be
        // removed; renaming a CLR event type must keep its original discriminator here.
        var frozen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WorkflowStartedEvent"] = "WorkflowStartedEvent",
            ["WorkflowContinuedAsNewEvent"] = "WorkflowContinuedAsNewEvent",
            ["WorkflowStepCompletedEvent"] = "WorkflowStepCompletedEvent",
            ["WorkflowStepFailedEvent"] = "WorkflowStepFailedEvent",
            ["WorkflowWaitRegisteredEvent"] = "WorkflowWaitRegisteredEvent",
            ["WorkflowWaitMatchedEvent"] = "WorkflowWaitMatchedEvent",
            ["WorkflowTimerScheduledEvent"] = "WorkflowTimerScheduledEvent",
            ["WorkflowTimerFiredEvent"] = "WorkflowTimerFiredEvent",
            ["WorkflowChildScheduledEvent"] = "WorkflowChildScheduledEvent",
            ["WorkflowChildrenScheduledEvent"] = "WorkflowChildrenScheduledEvent",
            ["WorkflowChildrenDispatchedEvent"] = "WorkflowChildrenDispatchedEvent",
            ["WorkflowChildCompletedEvent"] = "WorkflowChildCompletedEvent",
            ["WorkflowParentResumeTokenRecordedEvent"] = "WorkflowParentResumeTokenRecordedEvent",
            ["WorkflowParentResumeTokenConsumedEvent"] = "WorkflowParentResumeTokenConsumedEvent",
            ["WorkflowChildResidualIntentRecordedEvent"] = "WorkflowChildResidualIntentRecordedEvent",
            ["WorkflowChildCompensationScheduledEvent"] = "WorkflowChildCompensationScheduledEvent",
            ["WorkflowResourcePoolAcquiredEvent"] = "WorkflowResourcePoolAcquiredEvent",
            ["WorkflowResourcePoolQueuedEvent"] = "WorkflowResourcePoolQueuedEvent",
            ["WorkflowResourcePoolReleasedEvent"] = "WorkflowResourcePoolReleasedEvent",
            ["WorkflowExternalJobStartedEvent"] = "WorkflowExternalJobStartedEvent",
            ["WorkflowExternalJobCompletedEvent"] = "WorkflowExternalJobCompletedEvent",
            ["WorkflowExternalJobTimedOutEvent"] = "WorkflowExternalJobTimedOutEvent",
            ["WorkflowExternalJobStopRequestedEvent"] = "WorkflowExternalJobStopRequestedEvent",
            ["WorkflowTimerBufferedEvent"] = "WorkflowTimerBufferedEvent",
            ["WorkflowPausedEvent"] = "WorkflowPausedEvent",
            ["WorkflowResumedEvent"] = "WorkflowResumedEvent",
            ["WorkflowDeliveryBufferedEvent"] = "WorkflowDeliveryBufferedEvent",
            ["WorkflowDeliveryDiscardedEvent"] = "WorkflowDeliveryDiscardedEvent",
            ["WorkflowCompletedEvent"] = "WorkflowCompletedEvent",
            ["WorkflowTerminalEvent"] = "WorkflowTerminalEvent",
            ["SagaForwardActionCompletedEvent"] = "SagaForwardActionCompletedEvent",
            ["SagaForwardActionTimedOutEvent"] = "SagaForwardActionTimedOutEvent",
            ["SagaCompensationRequestedEvent"] = "SagaCompensationRequestedEvent",
            ["SagaCompensationStartedEvent"] = "SagaCompensationStartedEvent",
            ["SagaCompensationCompletedEvent"] = "SagaCompensationCompletedEvent",
            ["SagaCompensationFailedEvent"] = "SagaCompensationFailedEvent",
            ["SagaManualRecoveryRecordedEvent"] = "SagaManualRecoveryRecordedEvent"
        };

        var actual = WorkflowEventCodec.EventTypeNamesByClrTypeName;

        foreach (var (clrTypeName, eventType) in frozen)
        {
            actual.Should().Contain(
                new KeyValuePair<string, string>(clrTypeName, eventType),
                $"'{eventType}' is a persisted stream discriminator and may never change");
        }
    }

    public static TheoryData<WorkflowEvent> SupportedEvents
    {
        get
        {
            var events = new TheoryData<WorkflowEvent>();

            events.Add(Started(1));
            events.Add(new WorkflowContinuedAsNewEvent
            {
                EventId = EventIdValue(2),
                InstanceId = InstanceIdValue(2),
                CommandId = CommandIdValue(2),
                CausationId = CausationIdValue(2),
                OccurredAt = Timestamp(2),
                PreviousStreamVersion = new StreamVersion(7),
                Generation = 2
            });
            events.Add(new WorkflowStepCompletedEvent
            {
                EventId = EventIdValue(3),
                InstanceId = InstanceIdValue(3),
                CommandId = CommandIdValue(3),
                CausationId = CausationIdValue(3),
                OccurredAt = Timestamp(3),
                StepPath = "root/step"
            });
            events.Add(new WorkflowStepFailedEvent
            {
                EventId = EventIdValue(4),
                InstanceId = InstanceIdValue(4),
                CommandId = CommandIdValue(4),
                CausationId = CausationIdValue(4),
                OccurredAt = Timestamp(4),
                StepPath = "root/step",
                ErrorSummary = "failed"
            });
            events.Add(new WorkflowWaitRegisteredEvent
            {
                EventId = EventIdValue(5),
                InstanceId = InstanceIdValue(5),
                CommandId = CommandIdValue(5),
                CausationId = CausationIdValue(5),
                OccurredAt = Timestamp(5),
                WaitId = WaitIdValue(5),
                EventName = "approval",
                CorrelationId = CorrelationIdValue(5),
                Mode = WaitMode.Cold,
                BranchId = "branch-a"
            });
            events.Add(new WorkflowWaitMatchedEvent
            {
                EventId = EventIdValue(6),
                InstanceId = InstanceIdValue(6),
                CommandId = CommandIdValue(6),
                CausationId = CausationIdValue(6),
                OccurredAt = Timestamp(6),
                WaitId = WaitIdValue(6),
                MatchedEventId = EventIdValue(60)
            });
            events.Add(new WorkflowTimerScheduledEvent
            {
                EventId = EventIdValue(7),
                InstanceId = InstanceIdValue(7),
                CommandId = CommandIdValue(7),
                CausationId = CausationIdValue(7),
                OccurredAt = Timestamp(7),
                TimerId = TimerIdValue(7),
                FireAt = Timestamp(70),
                WakeupName = "timer"
            });
            events.Add(new WorkflowTimerFiredEvent
            {
                EventId = EventIdValue(8),
                InstanceId = InstanceIdValue(8),
                CommandId = CommandIdValue(8),
                CausationId = CausationIdValue(8),
                OccurredAt = Timestamp(8),
                TimerId = TimerIdValue(8)
            });
            events.Add(new WorkflowChildScheduledEvent
            {
                EventId = EventIdValue(9),
                InstanceId = InstanceIdValue(9),
                CommandId = CommandIdValue(9),
                CausationId = CausationIdValue(9),
                OccurredAt = Timestamp(9),
                ChildInstanceId = InstanceIdValue(90),
                ChildDefinitionId = DefinitionIdValue(9),
                ChildDefinitionVersion = DefinitionVersion.Initial,
                WaitId = WaitIdValue(9),
                FailurePolicy = RunChildFailurePolicy.ContinueParent
            });
            events.Add(new WorkflowChildrenScheduledEvent
            {
                EventId = EventIdValue(10),
                InstanceId = InstanceIdValue(10),
                CommandId = CommandIdValue(10),
                CausationId = CausationIdValue(10),
                OccurredAt = Timestamp(10),
                GroupId = "group",
                ChildDefinitionId = DefinitionIdValue(10),
                ChildDefinitionVersion = DefinitionVersion.Initial,
                FailurePolicy = RunChildFailurePolicy.PropagateFailure,
                JoinPolicy = RunChildrenJoinPolicy.WhenAll,
                ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining,
                TotalItemCount = 2,
                InitialDispatchCount = 1,
                NextDispatchIndex = 1,
                MaxConcurrency = 1,
                Children = [Child(0)]
            });
            events.Add(new WorkflowChildrenDispatchedEvent
            {
                EventId = EventIdValue(11),
                InstanceId = InstanceIdValue(11),
                CommandId = CommandIdValue(11),
                CausationId = CausationIdValue(11),
                OccurredAt = Timestamp(11),
                GroupId = "group",
                PreviousDispatchIndex = 1,
                NextDispatchIndex = 2,
                Children = [Child(1)]
            });
            events.Add(new WorkflowChildCompletedEvent
            {
                EventId = EventIdValue(12),
                InstanceId = InstanceIdValue(12),
                CommandId = CommandIdValue(12),
                CausationId = CausationIdValue(12),
                OccurredAt = Timestamp(12),
                ChildInstanceId = InstanceIdValue(120),
                ChildStatus = WorkflowStatus.Completed,
                ErrorSummary = null
            });
            events.Add(new WorkflowParentResumeTokenRecordedEvent
            {
                EventId = EventIdValue(13),
                InstanceId = InstanceIdValue(13),
                CommandId = CommandIdValue(13),
                CausationId = CausationIdValue(13),
                OccurredAt = Timestamp(13),
                GroupId = "group",
                ResumeTokenId = EventIdValue(130)
            });
            events.Add(new WorkflowParentResumeTokenConsumedEvent
            {
                EventId = EventIdValue(14),
                InstanceId = InstanceIdValue(14),
                CommandId = CommandIdValue(14),
                CausationId = CausationIdValue(14),
                OccurredAt = Timestamp(14),
                GroupId = "group",
                ResumeTokenId = EventIdValue(140)
            });
            events.Add(new WorkflowChildResidualIntentRecordedEvent
            {
                EventId = EventIdValue(15),
                InstanceId = InstanceIdValue(15),
                CommandId = CommandIdValue(15),
                CausationId = CausationIdValue(15),
                OccurredAt = Timestamp(15),
                GroupId = "group",
                ResidualPolicy = RunChildrenResidualPolicy.DetachRemaining,
                ResidualChildInstanceIds = [InstanceIdValue(151), InstanceIdValue(152)]
            });
            events.Add(new WorkflowChildCompensationScheduledEvent
            {
                EventId = EventIdValue(16),
                InstanceId = InstanceIdValue(16),
                CommandId = CommandIdValue(16),
                CausationId = CausationIdValue(16),
                OccurredAt = Timestamp(16),
                GroupId = "group",
                CompensationDefinitionId = DefinitionIdValue(16),
                CompensationDefinitionVersion = DefinitionVersion.Initial,
                Compensations = [Compensation(0)]
            });
            events.Add(new WorkflowResourcePoolAcquiredEvent
            {
                EventId = EventIdValue(17),
                InstanceId = InstanceIdValue(17),
                CommandId = CommandIdValue(17),
                CausationId = CausationIdValue(17),
                OccurredAt = Timestamp(17),
                HolderKey = "root/resource",
                Tickets = [Ticket(17)]
            });
            events.Add(new WorkflowResourcePoolQueuedEvent
            {
                EventId = EventIdValue(18),
                InstanceId = InstanceIdValue(18),
                CommandId = CommandIdValue(18),
                CausationId = CausationIdValue(18),
                OccurredAt = Timestamp(18),
                WaitId = WaitIdValue(18),
                HolderKey = "root/resource",
                Requirements = [new ResourcePoolRequirement("db", 1)],
                ExpiresAt = Timestamp(180)
            });
            events.Add(new WorkflowResourcePoolReleasedEvent
            {
                EventId = EventIdValue(19),
                InstanceId = InstanceIdValue(19),
                CommandId = CommandIdValue(19),
                CausationId = CausationIdValue(19),
                OccurredAt = Timestamp(19),
                HolderKey = "root/resource",
                Tickets = [Ticket(19)]
            });
            events.Add(new WorkflowExternalJobStartedEvent
            {
                EventId = EventIdValue(20),
                InstanceId = InstanceIdValue(20),
                CommandId = CommandIdValue(20),
                CausationId = CausationIdValue(20),
                OccurredAt = Timestamp(20),
                ExternalJobId = "job-1",
                Payload = [1, 2, 3],
                WaitId = WaitIdValue(20),
                TimeoutTimerId = TimerIdValue(20),
                TimeoutAt = Timestamp(200)
            });
            events.Add(new WorkflowExternalJobCompletedEvent
            {
                EventId = EventIdValue(21),
                InstanceId = InstanceIdValue(21),
                CommandId = CommandIdValue(21),
                CausationId = CausationIdValue(21),
                OccurredAt = Timestamp(21),
                ExternalJobId = "job-1",
                CompletionEventId = EventIdValue(210)
            });
            events.Add(new WorkflowExternalJobTimedOutEvent
            {
                EventId = EventIdValue(22),
                InstanceId = InstanceIdValue(22),
                CommandId = CommandIdValue(22),
                CausationId = CausationIdValue(22),
                OccurredAt = Timestamp(22),
                ExternalJobId = "job-1"
            });
            events.Add(new WorkflowExternalJobStopRequestedEvent
            {
                EventId = EventIdValue(23),
                InstanceId = InstanceIdValue(23),
                CommandId = CommandIdValue(23),
                CausationId = CausationIdValue(23),
                OccurredAt = Timestamp(23),
                ExternalJobId = "job-1"
            });
            events.Add(new WorkflowTimerBufferedEvent
            {
                EventId = EventIdValue(24),
                InstanceId = InstanceIdValue(24),
                CommandId = CommandIdValue(24),
                CausationId = CausationIdValue(24),
                OccurredAt = Timestamp(24),
                TimerId = TimerIdValue(24),
                WakeupName = "timer"
            });
            events.Add(new WorkflowPausedEvent
            {
                EventId = EventIdValue(25),
                InstanceId = InstanceIdValue(25),
                CommandId = CommandIdValue(25),
                CausationId = CausationIdValue(25),
                OccurredAt = Timestamp(25)
            });
            events.Add(new WorkflowResumedEvent
            {
                EventId = EventIdValue(26),
                InstanceId = InstanceIdValue(26),
                CommandId = CommandIdValue(26),
                CausationId = CausationIdValue(26),
                OccurredAt = Timestamp(26),
                BufferHandling = "discard"
            });
            events.Add(new WorkflowDeliveryBufferedEvent
            {
                EventId = EventIdValue(27),
                InstanceId = InstanceIdValue(27),
                CommandId = CommandIdValue(27),
                CausationId = CausationIdValue(27),
                OccurredAt = Timestamp(27),
                BufferedEventId = EventIdValue(270),
                EventName = "approval",
                CorrelationId = CorrelationIdValue(27),
                BranchId = "branch-b"
            });
            events.Add(new WorkflowDeliveryDiscardedEvent
            {
                EventId = EventIdValue(28),
                InstanceId = InstanceIdValue(28),
                CommandId = CommandIdValue(28),
                CausationId = CausationIdValue(28),
                OccurredAt = Timestamp(28),
                DiscardedEventId = EventIdValue(280)
            });
            events.Add(new WorkflowCompletedEvent
            {
                EventId = EventIdValue(29),
                InstanceId = InstanceIdValue(29),
                CommandId = CommandIdValue(29),
                CausationId = CausationIdValue(29),
                OccurredAt = Timestamp(29),
                OutcomeName = "ok"
            });
            events.Add(new WorkflowTerminalEvent
            {
                EventId = EventIdValue(30),
                InstanceId = InstanceIdValue(30),
                CommandId = CommandIdValue(30),
                CausationId = CausationIdValue(30),
                OccurredAt = Timestamp(30),
                Status = WorkflowStatus.Cancelled
            });
            events.Add(new SagaForwardActionCompletedEvent
            {
                EventId = EventIdValue(31),
                InstanceId = InstanceIdValue(31),
                CommandId = CommandIdValue(31),
                CausationId = CausationIdValue(31),
                OccurredAt = Timestamp(31),
                ScopeId = "saga",
                ActionKey = "reserve",
                CompensationKey = "release"
            });
            events.Add(new SagaForwardActionTimedOutEvent
            {
                EventId = EventIdValue(32),
                InstanceId = InstanceIdValue(32),
                CommandId = CommandIdValue(32),
                CausationId = CausationIdValue(32),
                OccurredAt = Timestamp(32),
                ScopeId = "saga",
                ActionKey = "reserve",
                CompensateScope = true
            });
            events.Add(new SagaCompensationRequestedEvent
            {
                EventId = EventIdValue(33),
                InstanceId = InstanceIdValue(33),
                CommandId = CommandIdValue(33),
                CausationId = CausationIdValue(33),
                OccurredAt = Timestamp(33),
                ScopeId = "saga",
                Reason = "rollback"
            });
            events.Add(new SagaCompensationStartedEvent
            {
                EventId = EventIdValue(34),
                InstanceId = InstanceIdValue(34),
                CommandId = CommandIdValue(34),
                CausationId = CausationIdValue(34),
                OccurredAt = Timestamp(34),
                ScopeId = "saga",
                ActionKey = "release",
                Order = 0
            });
            events.Add(new SagaCompensationCompletedEvent
            {
                EventId = EventIdValue(35),
                InstanceId = InstanceIdValue(35),
                CommandId = CommandIdValue(35),
                CausationId = CausationIdValue(35),
                OccurredAt = Timestamp(35),
                ScopeId = "saga",
                ActionKey = "release"
            });
            events.Add(new SagaCompensationFailedEvent
            {
                EventId = EventIdValue(36),
                InstanceId = InstanceIdValue(36),
                CommandId = CommandIdValue(36),
                CausationId = CausationIdValue(36),
                OccurredAt = Timestamp(36),
                ScopeId = "saga",
                ActionKey = "release",
                ErrorSummary = "failed"
            });
            events.Add(new SagaManualRecoveryRecordedEvent
            {
                EventId = EventIdValue(37),
                InstanceId = InstanceIdValue(37),
                CommandId = CommandIdValue(37),
                CausationId = CausationIdValue(37),
                OccurredAt = Timestamp(37),
                ScopeId = "saga",
                ActionKey = "release",
                OperatorId = "operator",
                RecoveryAction = "mark-complete",
                Reason = "manual correction",
                TargetStatus = WorkflowStatus.Completed
            });

            return events;
        }
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void ToEventType_UsesStableConcreteTypeName(WorkflowEvent workflowEvent)
    {
        WorkflowEventCodec.ToEventType(workflowEvent).Should().Be(workflowEvent.GetType().Name);
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void SerializeThenDeserialize_RoundTripsSupportedEvent(WorkflowEvent workflowEvent)
    {
        var eventType = WorkflowEventCodec.ToEventType(workflowEvent);
        var payload = WorkflowEventCodec.Serialize(workflowEvent);

        var decoded = WorkflowEventCodec.Deserialize(eventType, payload);

        decoded.GetType().Should().Be(workflowEvent.GetType());
        decoded.Should().BeEquivalentTo(workflowEvent);
    }

    [Fact]
    public void Deserialize_WhenTypeIsUnknown_Throws()
    {
        var act = () => WorkflowEventCodec.Deserialize("MissingEvent", "{}");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Workflow event type 'MissingEvent' is not supported.");
    }

    [Fact]
    public void Serialize_WhenEventIsUnknown_Throws()
    {
        var workflowEvent = new UnsupportedWorkflowEvent
        {
            EventId = EventIdValue(99),
            InstanceId = InstanceIdValue(99),
            CommandId = CommandIdValue(99),
            CausationId = CausationIdValue(99),
            OccurredAt = Timestamp(99)
        };

        var act = () => WorkflowEventCodec.Serialize(workflowEvent);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Workflow event 'UnsupportedWorkflowEvent' is not supported.");
    }

    private static WorkflowStartedEvent Started(int value)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(value),
            InstanceId = InstanceIdValue(value),
            CommandId = CommandIdValue(value),
            CausationId = CausationIdValue(value),
            OccurredAt = Timestamp(value),
            DefinitionId = DefinitionIdValue(value),
            DefinitionVersion = DefinitionVersion.Initial,
            IdempotencyKey = "start-key"
        };
    }

    private static WorkflowChildMaterialization Child(int index)
    {
        return new WorkflowChildMaterialization
        {
            Index = index,
            ChildInstanceId = InstanceIdValue(500 + index),
            ChildDefinitionId = DefinitionIdValue(500 + index),
            ChildDefinitionVersion = DefinitionVersion.Initial,
            ItemSnapshot = $"item-{index}"
        };
    }

    private static WorkflowChildCompensationMaterialization Compensation(int index)
    {
        return new WorkflowChildCompensationMaterialization
        {
            Index = index,
            SourceChildInstanceId = InstanceIdValue(600 + index),
            CompensationInstanceId = InstanceIdValue(700 + index),
            ItemSnapshot = $"item-{index}"
        };
    }

    private static ResourcePoolTicket Ticket(int value)
    {
        return new ResourcePoolTicket(
            GuidValue(800 + value),
            "db",
            1,
            InstanceIdValue(value),
            "root/resource",
            Timestamp(value),
            Timestamp(900 + value));
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

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static CorrelationId CorrelationIdValue(int value)
    {
        return new CorrelationId($"correlation-{value}");
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static DateTimeOffset Timestamp(int value)
    {
        return new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero).AddSeconds(value);
    }

    private sealed record UnsupportedWorkflowEvent : WorkflowEvent;
}
