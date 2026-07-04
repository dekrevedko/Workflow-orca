using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.ProviderCertification;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

[Trait("Category", "Certification")]
[Trait(Traits.Container, "SqlServer")]
public sealed class SqlServerResourcePoolCertificationTests : ResourcePoolStoreCertificationTests, IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();
    private SqlServerWorkflowStore? store;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        store = new SqlServerWorkflowStore(container.GetConnectionString());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (store is not null)
        {
            await store.DisposeAsync();
        }

        await container.DisposeAsync();
    }

    protected override IResourcePoolStore CreateStore()
    {
        return store ?? throw new InvalidOperationException("SQL Server resource-pool store is not initialized.");
    }

    [Fact]
    public async Task AcquireAsync_SplitStoreInstancesSharePoolCapacity()
    {
        var firstStore = RequiredStore();
        await firstStore.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await firstStore.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        await using var secondStore = new SqlServerWorkflowStore(container.GetConnectionString());
        await secondStore.InitializeAsync(TestContext.Current.CancellationToken);

        var secondAcquire = await secondStore.AcquireAsync(
            Request(2, Requirement("db")),
            TestContext.Current.CancellationToken);
        var secondSnapshot = await secondStore.GetPoolAsync("db", TestContext.Current.CancellationToken);

        secondAcquire.Status.Should().Be(ResourcePoolAcquireStatus.Queued);
        secondAcquire.Tickets.Should().BeEmpty();
        secondSnapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(1));
        secondSnapshot.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
    }

    [Fact]
    public async Task ReleaseAsync_SplitStoreInstancesGrantQueuedWaiter()
    {
        var firstStore = RequiredStore();
        await firstStore.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        await firstStore.AcquireAsync(Request(1, Requirement("db")), TestContext.Current.CancellationToken);

        await using var secondStore = new SqlServerWorkflowStore(container.GetConnectionString());
        await secondStore.InitializeAsync(TestContext.Current.CancellationToken);
        await secondStore.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);

        var release = await secondStore.ReleaseAsync(
            new ResourcePoolReleaseRequest(InstanceIdValue(1), "node-1", Timestamp(10)),
            TestContext.Current.CancellationToken);
        await using var thirdStore = new SqlServerWorkflowStore(container.GetConnectionString());
        await thirdStore.InitializeAsync(TestContext.Current.CancellationToken);
        var thirdSnapshot = await thirdStore.GetPoolAsync("db", TestContext.Current.CancellationToken);

        release.ReleasedTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(1));
        release.GrantedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        thirdSnapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        thirdSnapshot.Value.QueuedWaiters.Should().BeEmpty();
    }

    [Fact]
    public async Task ResourcePoolState_IsLoadedByNewStoreInstance()
    {
        var firstStore = RequiredStore();
        await firstStore.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquired = await firstStore.AcquireAsync(
            Request(1, Requirement("db")) with { ExpiresAt = Timestamp(5) },
            TestContext.Current.CancellationToken);
        await firstStore.AcquireAsync(Request(2, Requirement("db")), TestContext.Current.CancellationToken);
        await firstStore.ExpireTicketsAsync(Timestamp(6), TestContext.Current.CancellationToken);

        await using var restarted = new SqlServerWorkflowStore(container.GetConnectionString());
        await restarted.InitializeAsync(TestContext.Current.CancellationToken);

        var restartedSnapshot = await restarted.GetPoolAsync("db", TestContext.Current.CancellationToken);
        restartedSnapshot.HasValue.Should().BeTrue();
        restartedSnapshot.Value.Capacity.Should().Be(1);
        restartedSnapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.TicketId.Should().Be(acquired.Tickets.Single().TicketId);
        restartedSnapshot.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        restartedSnapshot.Value.ExpiredTickets.Should().ContainSingle()
            .Which.Ticket.TicketId.Should().Be(acquired.Tickets.Single().TicketId);

        var forced = await restarted.ForceReleaseTicketAsync(
            acquired.Tickets.Single().TicketId,
            "operator requested",
            Timestamp(7),
            TestContext.Current.CancellationToken);

        await using var audited = new SqlServerWorkflowStore(container.GetConnectionString());
        await audited.InitializeAsync(TestContext.Current.CancellationToken);
        var auditedSnapshot = await audited.GetPoolAsync("db", TestContext.Current.CancellationToken);

        forced.ReleasedTicket.Should().NotBeNull();
        forced.GrantedWaiters.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        auditedSnapshot.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderInstanceId.Should().Be(InstanceIdValue(2));
        auditedSnapshot.Value.AuditRecords.Should().ContainSingle()
            .Which.Reason.Should().Be("operator requested");
    }

    private SqlServerWorkflowStore RequiredStore()
    {
        return store ?? throw new InvalidOperationException("SQL Server resource-pool store is not initialized.");
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
            Timestamp(holder),
            Timestamp(holder).AddMinutes(30));
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 3, 16, minutes, 0, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }
}
