using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class ResourcePoolStoreCertificationTests
{
    protected abstract IResourcePoolStore CreateStore();

    [Fact]
    [Trait("AC", "AC-518")]
    [Trait("AC", "JS-AC-007")]
    public async Task AcquireAsync_WhenPoolHasCapacity_GrantsTicketAndReducesAvailableCapacity()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 2), TestContext.Current.CancellationToken);

        var result = await store.AcquireAsync(
            Request(1, Requirement("db")),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResourcePoolAcquireStatus.Granted);
        result.Tickets.Should().ContainSingle()
            .Which.PoolName.Should().Be("db");
        snapshot.Value.AvailableCapacity.Should().Be(1);
        snapshot.Value.HeldTickets.Should().ContainSingle();
    }

    [Fact]
    public async Task AcquireAsync_WhenPoolIsExhausted_QueuesWaiterWithoutGrantingTicket()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        var queued = await store.AcquireAsync(
            Request(2, Requirement("db")),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        queued.Status.Should().Be(ResourcePoolAcquireStatus.Queued);
        queued.Tickets.Should().BeEmpty();
        snapshot.Value.HeldTickets.Should().ContainSingle();
        snapshot.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
    }

    [Fact]
    [Trait("AC", "AC-522")]
    public async Task AcquireAsync_WhenMultiplePoolsRequested_GrantsAllOrNone()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db-a", 1), TestContext.Current.CancellationToken);
        await store.UpsertPoolAsync(Pool("db-b", 1), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db-b")), TestContext.Current.CancellationToken);

        var queued = await store.AcquireAsync(
            Request(2, Requirement("db-a"), Requirement("db-b")),
            TestContext.Current.CancellationToken);
        var dbA = await store.GetPoolAsync("db-a", TestContext.Current.CancellationToken);

        queued.Status.Should().Be(ResourcePoolAcquireStatus.Queued);
        queued.Tickets.Should().BeEmpty();
        dbA.Value.HeldTickets.Should().BeEmpty();
        dbA.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
    }

    [Fact]
    [Trait("AC", "AC-518")]
    public async Task ReleaseAsync_WhenTicketReleased_GrantsNextWaiterInFifoOrder()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var first = await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(3, Requirement("db")), TestContext.Current.CancellationToken);

        var release = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Date(10)),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        release.ReleasedTickets.Should().BeEquivalentTo(first.Tickets);
        release.GrantedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(3));
    }

    [Fact]
    [Trait("AC", "AC-521")]
    public async Task ExpireTicketsAsync_WhenTicketExpired_RecordsAudibleExpiryState()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await store.AcquireAsync(
            Request(1, Requirement("db")) with { ExpiresAt = Date(5) },
            TestContext.Current.CancellationToken);

        var expired = await store.ExpireTicketsAsync(Date(6), TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        expired.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
        snapshot.Value.HeldTickets.Should().ContainSingle();
        snapshot.Value.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
    }

    [Fact]
    public async Task ResizePoolAsync_WhenShrinkingBelowHeldCount_DoesNotRevokeHeldTickets()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 2), TestContext.Current.CancellationToken);
        await store.AcquireAsync(
            new ResourcePoolAcquireRequest(
                InstanceIdValue(1),
                "node-1",
                [new ResourcePoolRequirement("db", 2)],
                Date(1),
                Date(30)),
            TestContext.Current.CancellationToken);

        await store.ResizePoolAsync("db", 1, TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        snapshot.Value.Capacity.Should().Be(1);
        snapshot.Value.HeldTickets.Sum(ticket => ticket.Count).Should().Be(2);
        snapshot.Value.AvailableCapacity.Should().Be(0);
    }

    [Fact]
    public async Task ForceReleaseTicketAsync_WhenOperatorReleases_RecordsAuditAndGrantsNextWaiter()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);

        var forced = await store.ForceReleaseTicketAsync(
            acquired.Tickets.Single().TicketId,
            "operator requested",
            Date(7),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        forced.ReleasedTicket.Should().NotBeNull();
        forced.GrantedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.Value.AuditRecords.Should().ContainSingle()
            .Which.Reason.Should().Be("operator requested");
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolAcquireRequest Request(int holder, params ResourcePoolRequirement[] requirements)
    {
        return new ResourcePoolAcquireRequest(
            InstanceIdValue(holder),
            $"node-{holder}",
            requirements,
            Date(holder),
            Date(holder).AddMinutes(30));
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static DateTimeOffset Date(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 16, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}

public sealed class FakeResourcePoolStoreCertificationTests : ResourcePoolStoreCertificationTests
{
    protected override IResourcePoolStore CreateStore()
    {
        return new InMemoryResourcePoolStore();
    }
}
