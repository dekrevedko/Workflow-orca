using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.ResourcePools;

public sealed class PoolOperationsTests
{
    [Fact]
    [Trait("AC", "AC-521")]
    public async Task ExpireTicketsAsync_WhenTicketExpired_RecordsAudibleExpiryState()
    {
        var pools = new InMemoryResourcePoolStore();
        var management = new DurableManagement(new InMemoryWorkflowProvider(), pools);
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await pools.AcquireAsync(
            Request(1, Date(5)),
            TestContext.Current.CancellationToken);

        var expired = await management.ExpireResourcePoolTicketsAsync(Date(6), TestContext.Current.CancellationToken);
        var snapshot = await management.GetResourcePoolAsync("db", TestContext.Current.CancellationToken);

        expired.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
        snapshot.HeldTickets.Should().ContainSingle();
        snapshot.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
    }

    [Fact]
    public async Task ResizePool_WhenShrinkingBelowHeldCount_DoesNotRevokeHeldTickets()
    {
        var pools = new InMemoryResourcePoolStore();
        var management = new DurableManagement(new InMemoryWorkflowProvider(), pools);
        await pools.UpsertPoolAsync(Pool("db", 2), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                InstanceIdValue(1),
                "node-1",
                [new ResourcePoolRequirement("db", 2)],
                Date(1),
                Date(30)),
            TestContext.Current.CancellationToken);

        await management.ResizeResourcePoolAsync("db", 1, TestContext.Current.CancellationToken);
        var snapshot = await management.GetResourcePoolAsync("db", TestContext.Current.CancellationToken);

        snapshot.Capacity.Should().Be(1);
        snapshot.HeldTickets.Sum(ticket => ticket.Count).Should().Be(2);
        snapshot.AvailableCapacity.Should().Be(0);
    }

    [Fact]
    public async Task ForceReleaseTicket_WhenOperatorReleases_RecordsAuditAndGrantsNextWaiter()
    {
        var pools = new InMemoryResourcePoolStore();
        var management = new DurableManagement(new InMemoryWorkflowProvider(), pools);
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await pools.AcquireAsync(Request(1, Date(30)), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(Request(2, Date(30)), TestContext.Current.CancellationToken);

        var forced = await management.ForceReleaseResourcePoolTicketAsync(
            acquired.Tickets.Single().TicketId,
            "operator requested",
            Date(7),
            TestContext.Current.CancellationToken);
        var snapshot = await management.GetResourcePoolAsync("db", TestContext.Current.CancellationToken);

        forced.GrantedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.AuditRecords.Should().ContainSingle()
            .Which.Reason.Should().Be("operator requested");
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolAcquireRequest Request(int holder, DateTimeOffset expiresAt)
    {
        return new ResourcePoolAcquireRequest(
            InstanceIdValue(holder),
            $"node-{holder}",
            [new ResourcePoolRequirement("db", 1)],
            Date(holder),
            expiresAt);
    }

    private static DateTimeOffset Date(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 18, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
