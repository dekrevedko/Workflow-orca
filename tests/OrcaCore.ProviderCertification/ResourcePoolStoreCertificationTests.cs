using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting.ResourceLeases;
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
    public async Task Ownership_RoundTripsAcrossGrantQueueAndReleaseGrant()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var firstRequest = Request(1, Requirement("db")) with
        {
            FiberId = new FiberId("fiber-1"),
            ScopeId = new ScopeId("scope-1")
        };
        var secondRequest = Request(2, Requirement("db")) with
        {
            FiberId = new FiberId("fiber-2"),
            ScopeId = new ScopeId("scope-2")
        };

        var first = await store.AcquireAsync(firstRequest, TestContext.Current.CancellationToken);
        var queued = await store.AcquireAsync(secondRequest, TestContext.Current.CancellationToken);

        first.Tickets.Single().FiberId.Should().Be(firstRequest.FiberId);
        first.Tickets.Single().ScopeId.Should().Be(firstRequest.ScopeId);
        queued.QueuedWaiter!.FiberId.Should().Be(secondRequest.FiberId);
        queued.QueuedWaiter.ScopeId.Should().Be(secondRequest.ScopeId);

        var release = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Date(10)),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        release.GrantedWaiters.Single().FiberId.Should().Be(secondRequest.FiberId);
        release.GrantedWaiters.Single().ScopeId.Should().Be(secondRequest.ScopeId);
        snapshot.Value.HeldTickets.Single().FiberId.Should().Be(secondRequest.FiberId);
        snapshot.Value.HeldTickets.Single().ScopeId.Should().Be(secondRequest.ScopeId);
    }

    [Fact]
    public async Task ListPoolsAsync_ReturnsAllPoolSnapshotsWithTicketsAndWaiters()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await store.UpsertPoolAsync(Pool("cpu", 2), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);

        var snapshots = await store.ListPoolsAsync(TestContext.Current.CancellationToken);

        snapshots.Select(pool => pool.Name).Should().BeEquivalentTo("db", "cpu");
        var db = snapshots.Single(pool => pool.Name == "db");
        db.Capacity.Should().Be(1);
        db.AvailableCapacity.Should().Be(0);
        db.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(1));
        db.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshots.Single(pool => pool.Name == "cpu")
            .AvailableCapacity.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "AC-518")]
    [Trait("AC", "AC-519")]
    public async Task AcquireAsync_ConcurrentRequestsForLastSlot_GrantsOneAndQueuesOne()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);

        var first = store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        var second = store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        results.Count(result => result.Status == ResourcePoolAcquireStatus.Granted).Should().Be(1);
        results.Count(result => result.Status == ResourcePoolAcquireStatus.Queued).Should().Be(1);
        snapshot.Value.HeldTickets.Sum(ticket => ticket.Count).Should().Be(1);
        snapshot.Value.QueuedWaiters.Should().ContainSingle();
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
        await store.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
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
    public void ProviderPort_DoesNotExposeLegacyRawStringResize()
    {
        typeof(IResourcePoolStore).GetMethod("ResizePoolAsync").Should().BeNull();
    }

    [Fact]
    public void ResourceAdministration_UsesTheStrongIdentityOperationBoundContract()
    {
        var resize = typeof(IDurableResourcePoolManagement).GetMethod("ResizeAsync");

        resize.Should().NotBeNull();
        resize!.GetParameters().Select(parameter => parameter.ParameterType.Name).Should().Equal(
            "ResourcePoolName",
            "Int32",
            "ResourcePoolOperationId",
            "CancellationToken");
        resize.ReturnType.Name.Should().StartWith("ValueTask");
        resize.ReturnType.GenericTypeArguments.Should().ContainSingle()
            .Which.Name.Should().Be("DurableResourcePoolResizeResult");
    }

    [Fact]
    public async Task UpsertPoolAsync_ReplayValidatesTheImmutableCreationDefinition()
    {
        var store = CreateStore();
        var creation = Pool("db", 3);
        await store.UpsertPoolAsync(creation, TestContext.Current.CancellationToken);

        await store.UpsertPoolAsync(creation, TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);
        snapshot.Value.Capacity.Should().Be(3);

        await store.Invoking(candidate => candidate.UpsertPoolAsync(
                Pool("db", 1),
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>();
        (await store.GetPoolAsync("db", TestContext.Current.CancellationToken))
            .Value.Capacity.Should().Be(3);
    }

    [Fact]
    public async Task ReleaseAsync_QueuedHolder_CancelsWaiterWithoutAllocatingTickets()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);

        var cancelled = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(2), "node-2", Date(10)),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        cancelled.ReleasedTickets.Should().BeEmpty();
        cancelled.GrantedWaiters.Should().BeEmpty();
        snapshot.Value.QueuedWaiters.Should().BeEmpty();
        snapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(1));
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-011")]
    [Trait("AC", "MG-062")]
    public async Task NEG_MG_011_AcquireAsync_UnknownPoolRejectsWithoutTicket()
    {
        var store = CreateStore();

        var result = await store.AcquireAsync(
            Request(1, Requirement("pool-does-not-exist")),
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResourcePoolAcquireStatus.Rejected);
        result.Tickets.Should().BeEmpty();
        result.QueuedWaiter.Should().BeNull();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-012")]
    [Trait("AC", "AC-518")]
    public async Task NEG_MG_012_AcquireAsync_CapacityZeroPoolNeverGrantsTicket()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 0), TestContext.Current.CancellationToken);

        var result = await store.AcquireAsync(
            Request(1, Requirement("db")),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Status.Should().NotBe(ResourcePoolAcquireStatus.Granted);
        result.Tickets.Should().BeEmpty();
        snapshot.Value.AvailableCapacity.Should().Be(0);
        snapshot.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-013")]
    [Trait("AC", "AC-520")]
    public async Task NEG_MG_013_ReleaseAsync_UnknownHolderDoesNotChangeCapacity()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        var release = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(404), "missing-holder", Date(10)),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        release.ReleasedTickets.Should().BeEmpty();
        release.GrantedWaiters.Should().BeEmpty();
        snapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(1));
        snapshot.Value.AvailableCapacity.Should().Be(0);
    }

    [Fact]
    [Trait("Scenario", "NEG-MG-014")]
    [Trait("AC", "AC-520")]
    public async Task NEG_MG_014_ReleaseAsync_DoubleReleaseDoesNotIncrementCapacityTwice()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        var firstRelease = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Date(10)),
            TestContext.Current.CancellationToken);
        var secondRelease = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Date(11)),
            TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        firstRelease.ReleasedTickets.Should().BeEquivalentTo(acquired.Tickets);
        secondRelease.ReleasedTickets.Should().BeEmpty();
        snapshot.Value.HeldTickets.Should().BeEmpty();
        snapshot.Value.AvailableCapacity.Should().Be(1);
    }

    [Fact]
    [Trait("Scenario", "NEG-PR-018")]
    [Trait("AC", "NF-040")]
    public async Task NEG_PR_018_ResourcePoolNameWithSqlMetacharactersIsTreatedAsData()
    {
        var store = CreateStore();
        const string maliciousPoolName = "db'; drop table orcacore_resource_pools; --";
        await store.UpsertPoolAsync(Pool(maliciousPoolName, 1), TestContext.Current.CancellationToken);
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);

        var result = await store.AcquireAsync(
            Request(1, Requirement(maliciousPoolName)),
            TestContext.Current.CancellationToken);
        var maliciousSnapshot = await store.GetPoolAsync(maliciousPoolName, TestContext.Current.CancellationToken);
        var normalSnapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Status.Should().Be(ResourcePoolAcquireStatus.Granted);
        result.Tickets.Should().ContainSingle()
            .Which.PoolName.Should().Be(maliciousPoolName);
        maliciousSnapshot.HasValue.Should().BeTrue();
        normalSnapshot.HasValue.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "DR-AC-022")]
    public async Task AcquireAsync_HolderAlreadyGranted_IsIdempotentAndDoesNotQueue()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var first = await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        var again = await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        first.Status.Should().Be(ResourcePoolAcquireStatus.Granted);
        again.Status.Should().Be(
            ResourcePoolAcquireStatus.Granted,
            "an at-least-once re-acquire by a granted holder observes its existing tickets");
        again.Tickets.Select(ticket => ticket.TicketId)
            .Should().BeEquivalentTo(first.Tickets.Select(ticket => ticket.TicketId));
        snapshot.Value.HeldTickets.Should().ContainSingle("no second allocation happens");
        snapshot.Value.QueuedWaiters.Should().BeEmpty("a granted holder never queues behind its own tickets");
    }

    [Fact]
    [Trait("AC", "DR-AC-022")]
    public async Task AcquireAsync_AfterReleaseGrantsQueuedWaiter_ReacquireObservesGrant()
    {
        var store = CreateStore();
        await store.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await store.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);
        var queued = await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);
        queued.Status.Should().Be(ResourcePoolAcquireStatus.Queued);

        var release = await store.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Date(20)),
            TestContext.Current.CancellationToken);
        var reacquired = await store.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);
        var snapshot = await store.GetPoolAsync("db", TestContext.Current.CancellationToken);

        release.GrantedWaiters.Should().ContainSingle(
            "the released capacity advances the queued waiter");
        reacquired.Status.Should().Be(
            ResourcePoolAcquireStatus.Granted,
            "the grant-signaled holder re-attempts its acquisition and observes the grant");
        reacquired.Tickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        snapshot.Value.HeldTickets.Should().ContainSingle();
        snapshot.Value.QueuedWaiters.Should().BeEmpty();
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
        return InstanceId.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}

public sealed class InMemoryResourcePoolStoreCertificationTests : ResourcePoolStoreCertificationTests
{
    protected override IResourcePoolStore CreateStore()
    {
        return InMemoryProviderPorts.Create().ResourcePoolStore;
    }
}
