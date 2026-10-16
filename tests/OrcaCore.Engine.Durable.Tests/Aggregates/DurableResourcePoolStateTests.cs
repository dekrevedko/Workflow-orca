using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableResourcePoolStateTests
{
    [Fact]
    public void PlanAcquire_WhenGranted_RecordsHeldTicketsWithoutEviction()
    {
        var state = DurableResourcePoolState.FromSnapshot([]);

        var plan = state.PlanAcquire(
            ResourcePoolContext(CommandIdValue(10), Timestamp(10)),
            "node-1",
            [Requirement("db")],
            Timestamp(40),
            new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Granted, [Ticket("node-1", "db", 1)], null, null));

        plan.EvictAfterCommit.Should().BeFalse();
        plan.Events.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle()
            .Which.Tickets.Should().ContainSingle()
            .Which.Should().Be(Ticket("node-1", "db", 1));
    }

    [Fact]
    public void PlanAcquire_WhenQueued_RecordsColdWaitIntentAndEvicts()
    {
        var state = DurableResourcePoolState.FromSnapshot([]);
        var waiter = new ResourcePoolWaiter(
            GuidValue(100),
            InstanceIdValue(1),
            "node-1",
            [Requirement("db")],
            Timestamp(10),
            Timestamp(40));

        var plan = state.PlanAcquire(
            ResourcePoolContext(CommandIdValue(10), Timestamp(10)),
            "node-1",
            [Requirement("db")],
            Timestamp(40),
            new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Queued, [], waiter, null));

        plan.EvictAfterCommit.Should().BeTrue();
        var queued = plan.Events.OfType<WorkflowResourcePoolQueuedEvent>().Should().ContainSingle().Subject;
        queued.HolderKey.Should().Be("node-1");
        queued.Requirements.Should().Equal(Requirement("db"));
        queued.ExpiresAt.Should().Be(Timestamp(40));
    }

    [Fact]
    public void CreateReleaseEvents_GroupsTicketsByHolderAndCanFilter()
    {
        var state = DurableResourcePoolState.FromSnapshot(
            [
                Ticket("node-1", "db", 1),
                Ticket("node-1", "cpu", 1),
                Ticket("node-2", "db", 1)
            ]);

        var all = state.CreateReleaseEvents(ResourcePoolContext(CommandIdValue(10), Timestamp(10)));
        var filtered = state.CreateReleaseEvents(ResourcePoolContext(CommandIdValue(11), Timestamp(11)), "node-1");

        all.Select(released => released.HolderKey).Should().Equal("node-1", "node-2");
        filtered.Should().ContainSingle()
            .Which.Tickets.Select(ticket => ticket.PoolName).Should().Equal("db", "cpu");
    }

    [Fact]
    public void ApplyQueued_ReturnsColdResourcePoolGrantedWait()
    {
        var state = DurableResourcePoolState.FromSnapshot([]);

        var effects = state.Apply(new WorkflowResourcePoolQueuedEvent
        {
            EventId = EventIdValue(20),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(20),
            CausationId = CausationIdValue(20),
            OccurredAt = Timestamp(20),
            WaitId = WaitIdValue(20),
            HolderKey = "node-1",
            Requirements = [Requirement("db")],
            ExpiresAt = Timestamp(40)
        });

        var wait = effects.WaitsToRegister.Should().ContainSingle().Subject;
        wait.WaitId.Should().Be(WaitIdValue(20));
        wait.EventName.Should().Be("ResourcePoolGranted");
        wait.CorrelationId.Should().Be(CorrelationId.Create("node-1"));
        wait.Mode.Should().Be(WaitMode.Cold);
    }

    [Fact]
    public void ApplyAcquiredThenReleased_TracksActiveTickets()
    {
        var state = DurableResourcePoolState.FromSnapshot([]);

        state.Apply(Acquired("node-1", Ticket("node-1", "db", 1)));
        state.ActiveTickets.Should().ContainSingle().Which.HolderKey.Should().Be("node-1");

        state.Apply(Released("node-1", Ticket("node-1", "db", 1)));

        state.ActiveTickets.Should().BeEmpty();
    }

    [Fact]
    public void CreateCheckpointActiveResourceTickets_RoundTripsActiveTickets()
    {
        var state = DurableResourcePoolState.FromSnapshot([Ticket("node-1", "db", 1)]);

        var tickets = state.CreateCheckpointActiveResourceTickets();

        tickets.Should().ContainSingle().Which.Should().Be(Ticket("node-1", "db", 1));
    }

    private static WorkflowResourcePoolAcquiredEvent Acquired(string holderKey, params ResourcePoolTicket[] tickets)
    {
        return new WorkflowResourcePoolAcquiredEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static WorkflowResourcePoolReleasedEvent Released(string holderKey, params ResourcePoolTicket[] tickets)
    {
        return new WorkflowResourcePoolReleasedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static DurableResourcePoolEventContext ResourcePoolContext(
        CommandId commandId,
        DateTimeOffset requestedAt)
    {
        return new DurableResourcePoolEventContext(
            commandId,
            InstanceIdValue(1),
            requestedAt,
            ParentInstanceId: null,
            InstanceIdValue(1));
    }

    private static ResourcePoolTicket Ticket(string holderKey, string poolName, int count)
    {
        return new ResourcePoolTicket(
            GuidValue(TicketNumber(holderKey, poolName, count)),
            poolName,
            count,
            InstanceIdValue(1),
            holderKey,
            Timestamp(1),
            Timestamp(40));
    }

    private static int TicketNumber(string holderKey, string poolName, int count)
    {
        return (holderKey, poolName, count) switch
        {
            ("node-1", "db", 1) => 1,
            ("node-1", "cpu", 1) => 2,
            ("node-2", "db", 1) => 3,
            _ => 99
        };
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 3, 16, minutes, 0, TimeSpan.Zero);
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

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
