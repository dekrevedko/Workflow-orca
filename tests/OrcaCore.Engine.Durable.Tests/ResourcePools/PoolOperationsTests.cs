using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
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
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await pools.AcquireAsync(
            Request(1, Date(5)),
            TestContext.Current.CancellationToken);

        var expired = await pools.ExpireTicketsAsync(Date(32), TestContext.Current.CancellationToken);
        var snapshot = (await pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value;

        expired.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
        snapshot.HeldTickets.Should().ContainSingle();
        snapshot.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
    }

    [Fact]
    public void ResourcePoolProviderPort_DoesNotExposeLegacyRawStringResize()
    {
        typeof(IResourcePoolStore).GetMethod("ResizePoolAsync").Should().BeNull(
            "resource administration belongs to the strong-ID management contract");
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
        return InstanceId.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
