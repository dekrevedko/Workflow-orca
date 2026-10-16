using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

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
            ["WorkflowWaitCancelledEvent"] = "WorkflowWaitCancelledEvent",
            ["WorkflowTimerCancelledEvent"] = "WorkflowTimerCancelledEvent",
            ["WorkflowResumeConsumedEvent"] = "WorkflowResumeConsumedEvent",
            ["WorkflowParkedEvent"] = "WorkflowParkedEvent",
            ["WorkflowUnparkedEvent"] = "WorkflowUnparkedEvent",
            ["WorkflowContinuationAttemptFailedEvent"] = "WorkflowContinuationAttemptFailedEvent",
            ["WorkflowContinuationAttemptResetEvent"] = "WorkflowContinuationAttemptResetEvent",
            ["WorkflowTimerScheduledEvent"] = "WorkflowTimerScheduledEvent",
            ["WorkflowTimerFiredEvent"] = "WorkflowTimerFiredEvent",
            ["WorkflowResourcePoolAcquiredEvent"] = "WorkflowResourcePoolAcquiredEvent",
            ["WorkflowResourcePoolQueuedEvent"] = "WorkflowResourcePoolQueuedEvent",
            ["WorkflowResourcePoolReleasedEvent"] = "WorkflowResourcePoolReleasedEvent",
            ["WorkflowCompletedEvent"] = "WorkflowCompletedEvent",
            ["WorkflowCancellationRequestedEvent"] = "WorkflowCancellationRequestedEvent",
            ["WorkflowTerminalEvent"] = "WorkflowTerminalEvent"
        };

        var actual = WorkflowEventCodec.EventTypeNamesByClrTypeName;
        var concreteEventTypeNames = typeof(DurableWorkflowEvent).Assembly
            .GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(DurableWorkflowEvent).IsAssignableFrom(type))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        actual.Should().BeEquivalentTo(frozen);
        actual.Keys.Should().BeEquivalentTo(concreteEventTypeNames);
    }

    public static TheoryData<DurableWorkflowEvent> SupportedEvents
    {
        get
        {
            var events = new TheoryData<DurableWorkflowEvent>();

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
            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventIdValue(9),
                InstanceId = InstanceIdValue(9),
                CommandId = CommandIdValue(9),
                CausationId = CausationIdValue(9),
                OccurredAt = Timestamp(9),
                WaitId = WaitIdValue(9)
            });
            events.Add(new WorkflowTimerCancelledEvent
            {
                EventId = EventIdValue(10),
                InstanceId = InstanceIdValue(10),
                CommandId = CommandIdValue(10),
                CausationId = CausationIdValue(10),
                OccurredAt = Timestamp(10),
                TimerId = TimerIdValue(10)
            });
            events.Add(new WorkflowResumeConsumedEvent
            {
                EventId = EventIdValue(11),
                InstanceId = InstanceIdValue(11),
                CommandId = CommandIdValue(11),
                CausationId = CausationIdValue(11),
                OccurredAt = Timestamp(11),
                WaitId = WaitIdValue(11)
            });
            events.Add(new WorkflowParkedEvent
            {
                EventId = EventIdValue(12),
                InstanceId = InstanceIdValue(12),
                CommandId = CommandIdValue(12),
                CausationId = CausationIdValue(12),
                OccurredAt = Timestamp(12),
                Reason = DurableParkReason.Poison,
                ErrorSummary = "parked",
                FailedAttemptCount = 3,
                PositionStreamVersion = new StreamVersion(7)
            });
            events.Add(new WorkflowUnparkedEvent
            {
                EventId = EventIdValue(13),
                InstanceId = InstanceIdValue(13),
                CommandId = CommandIdValue(13),
                CausationId = CausationIdValue(13),
                OccurredAt = Timestamp(13)
            });
            events.Add(new WorkflowContinuationAttemptFailedEvent
            {
                EventId = EventIdValue(14),
                InstanceId = InstanceIdValue(14),
                CommandId = CommandIdValue(14),
                CausationId = CausationIdValue(14),
                OccurredAt = Timestamp(14),
                AttemptCount = 2,
                PositionStreamVersion = new StreamVersion(8),
                NextEligibleAt = Timestamp(140),
                ErrorSummary = "retryable"
            });
            events.Add(new WorkflowContinuationAttemptResetEvent
            {
                EventId = EventIdValue(15),
                InstanceId = InstanceIdValue(15),
                CommandId = CommandIdValue(15),
                CausationId = CausationIdValue(15),
                OccurredAt = Timestamp(15)
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
            events.Add(new WorkflowCompletedEvent
            {
                EventId = EventIdValue(29),
                InstanceId = InstanceIdValue(29),
                CommandId = CommandIdValue(29),
                CausationId = CausationIdValue(29),
                OccurredAt = Timestamp(29),
                OutcomeName = "ok"
            });
            events.Add(new WorkflowCancellationRequestedEvent
            {
                EventId = EventIdValue(28),
                InstanceId = InstanceIdValue(28),
                CommandId = CommandIdValue(28),
                CausationId = CausationIdValue(28),
                OccurredAt = Timestamp(28)
            });
            events.Add(new WorkflowTerminalEvent
            {
                EventId = EventIdValue(30),
                InstanceId = InstanceIdValue(30),
                CommandId = CommandIdValue(30),
                CausationId = CausationIdValue(30),
                OccurredAt = Timestamp(30),
                Status = global::OrcaCore.WorkflowInstanceStatus.Cancelled
            });
            return events;
        }
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void ToEventType_UsesStableConcreteTypeName(DurableWorkflowEvent workflowEvent)
    {
        WorkflowEventCodec.ToEventType(workflowEvent).Should().Be(workflowEvent.GetType().Name);
    }

    [Theory]
    [MemberData(nameof(SupportedEvents))]
    public void SerializeThenDeserialize_RoundTripsSupportedEvent(DurableWorkflowEvent workflowEvent)
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

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static CorrelationId CorrelationIdValue(int value)
    {
        return CorrelationId.Create($"correlation-{value}");
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private static DateTimeOffset Timestamp(int value)
    {
        return new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero).AddSeconds(value);
    }

    private sealed record UnsupportedWorkflowEvent : DurableWorkflowEvent;
}
