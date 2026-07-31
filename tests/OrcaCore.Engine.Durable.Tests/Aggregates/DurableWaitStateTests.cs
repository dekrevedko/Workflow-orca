using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableWaitStateTests
{
    [Fact]
    public void FindActiveWait_WhenOwnersReuseCorrelation_SelectsLowestSequenceThenFiberId()
    {
        var state = DurableWaitState.FromSnapshot(
            [
                ActiveWait(1, "checkout:a") with
                {
                    WaitSequence = 8,
                    FiberId = new FiberId("fiber-b")
                },
                ActiveWait(2, "checkout:b") with
                {
                    WaitSequence = 7,
                    FiberId = new FiberId("fiber-z")
                },
                ActiveWait(3, "checkout:c") with
                {
                    WaitSequence = 7,
                    FiberId = new FiberId("fiber-a")
                }
            ],
            []);

        var match = state.FindActiveWait(new EventEnvelope
        {
            EventId = EventIdValue(10),
            EventName = "Approved",
            CorrelationId = CorrelationId.Create("order-1"),
            OccurredAt = Timestamp(10)
        });

        match.Should().NotBeNull();
        match!.WaitId.Should().Be(WaitIdValue(3));
    }

    [Fact]
    public void PlanBufferedDeliveryReplay_WhenBufferedDeliveryMatchesWait_EmitsMatchAndAppliedInboxWrite()
    {
        var eventId = EventIdValue(20);
        var state = DurableWaitState.FromSnapshot(
            [ActiveWait(1, "checkout:a")],
            [BufferedDelivery(eventId, "checkout:a")]);

        var plan = state.PlanBufferedDeliveryReplay(
            WaitEventContext(CommandIdValue(30), Timestamp(30)),
            ResumeBufferedDeliveries.Replay);

        plan.Events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.WaitId.Should().Be(WaitIdValue(1));
        plan.InboxWrites.Should().ContainSingle()
            .Which.Should().Be(new InboxWrite(eventId, InboxRecordState.Applied));
    }

    [Fact]
    public void PlanBufferedDeliveryReplay_WhenDiscardingBufferedDeliveries_EmitsDiscardAndInboxDiscard()
    {
        var eventId = EventIdValue(20);
        var state = DurableWaitState.FromSnapshot(
            [ActiveWait(1, "checkout:a")],
            [BufferedDelivery(eventId, "checkout:a")]);

        var plan = state.PlanBufferedDeliveryReplay(
            WaitEventContext(CommandIdValue(30), Timestamp(30)),
            ResumeBufferedDeliveries.Discard);

        plan.Events.OfType<WorkflowDeliveryDiscardedEvent>().Should().ContainSingle()
            .Which.DiscardedEventId.Should().Be(eventId);
        plan.Events.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
        plan.InboxWrites.Should().ContainSingle()
            .Which.Should().Be(new InboxWrite(eventId, InboxRecordState.DiscardedOnResume));
    }

    private static DurableActiveWait ActiveWait(int value, string? branchId = null)
    {
        return new DurableActiveWait(
            WaitIdValue(value),
            "Approved",
            CorrelationId.Create("order-1"),
            Timestamp(value),
            WaitMode.Resident,
            branchId);
    }

    private static DurableBufferedDelivery BufferedDelivery(EventId eventId, string? branchId = null)
    {
        return new DurableBufferedDelivery(
            eventId,
            "Approved",
            CorrelationId.Create("order-1"),
            branchId);
    }

    private static DurableWaitEventContext WaitEventContext(CommandId commandId, DateTimeOffset requestedAt)
    {
        return new DurableWaitEventContext(commandId, InstanceIdValue(1), requestedAt);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 13, 0, seconds, TimeSpan.Zero);
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

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
