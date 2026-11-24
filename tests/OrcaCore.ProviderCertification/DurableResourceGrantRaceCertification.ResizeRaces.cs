using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Hosting.Tests;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static partial class DurableResourceGrantRaceCertification
{
    public enum ResizeRace { BlockedHead, OwnerCancels, HolderReleases }

    public static async Task ResizeRaceAsync(Func<IServiceCollection> services, Type providerType, ResizeRace schedule)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        var ct = deadline.Token;
        var partition = ResourceGovernancePartitionId.Create($"resize-race-{Guid.CreateVersion7():N}");
        await using var host = BuildHost(services(), partition);
        await host.GetServices<IHostedService>().Single(s => s.GetType().Assembly == providerType.Assembly).StartAsync(ct);
        var pools = host.GetRequiredService<IResourcePoolStore>();
        var events = host.GetRequiredService<IWorkflowEventStore>();
        var governance = host.GetRequiredService<IDurableResourceGovernanceStore>();
        var blocker = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var follower = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var held = Assert.Single((await pools.AcquireAsync(Request(blocker, "blocker"), ct)).Tickets);
        var slot = ResourcePoolName.Create("slot");
        var headUnits = schedule == ResizeRace.BlockedHead ? 2 : 1;
        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(value => value)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(slot, headUnits)),
                scope => scope.Then<CountedStep>()).End().Build();
        var runtime = CreateRuntime(host, NullDurableResourceLeaseCertificationGate.Instance);
        runtime.RegisterDefinition(definition);
        var owner = (await runtime.StartOrGetAsync<int, int>($"owner-{partition}", definition.DefinitionId,
            definition.DefinitionVersion, 0, ct)).InstanceId;
        Assert.Equal(ResourcePoolAcquireStatus.Queued, (await pools.AcquireAsync(Request(follower, "follower"), ct)).Status);
        var queued = (await pools.GetPoolAsync("slot", ct)).Value;
        queued.QueuedWaiters.Select(w => w.HolderInstanceId).Should().Equal(owner, follower);

        if (schedule == ResizeRace.BlockedHead)
        {
            var management = host.GetRequiredService<IDurableResourcePoolManagement>();
            var partial = Assert.IsType<DurableResourcePoolResizeResult.Applied>(
                await management.ResizeAsync(slot, 2, ResourcePoolOperationId.Create("partial"), ct));
            Assert.Equal(1, partial.Snapshot.ReservedUnits);
            Assert.Equal(2, partial.Snapshot.QueuedRequestCount);
            var blocked = (await pools.GetPoolAsync("slot", ct)).Value;
            Assert.Equal(1, blocked.AvailableCapacity);
            Assert.Equal(held.TicketId, Assert.Single(blocked.HeldTickets).TicketId);
            blocked.QueuedWaiters.Select(w => w.WaiterId).Should().Equal(queued.QueuedWaiters.Select(w => w.WaiterId));

            var release = await pools.ReleaseAsync(new ResourcePoolReleaseRequest(blocker, "blocker", DateTimeOffset.UtcNow), ct);
            Assert.Equal(owner, Assert.Single(release.GrantedWaiters).HolderInstanceId);
            var after = (await pools.GetPoolAsync("slot", ct)).Value;
            var promoted = Assert.Single(after.HeldTickets);
            Assert.Equal(owner, promoted.HolderInstanceId);
            Assert.Equal(2, promoted.Count);
            Assert.Equal(follower, Assert.Single(after.QueuedWaiters).HolderInstanceId);
            return;
        }

        var baseline = (await governance.LoadLatestAsync(partition, ct)).Version;
        ResourcePoolTicket? transferred = null;
        var reads = 0;
        var resizeReader = new DagEventStoreProbe
        {
            Inner = events,
            AfterCheckpointRead = async token =>
            {
                if (++reads != 1) return;
                // The resize holds its detached read while the other host commits.
                if (schedule == ResizeRace.OwnerCancels)
                {
                    var stopAfterCancel = new DagEventStoreProbe
                    {
                        Inner = events,
                        AfterAppend = batch =>
                        {
                            if (batch.Events.Any(e => e is WorkflowCancellationRequestedEvent)) throw new WorkerStopped();
                        }
                    };
                    var canceller = new DurableCommandProcessor(stopAfterCancel, pools);
                    await Assert.ThrowsAsync<WorkerStopped>(() => canceller.ProcessAsync(new CancelWorkflowCommand
                    {
                        InstanceId = owner, CommandId = CommandId.New(), RequestedAt = DateTimeOffset.UtcNow
                    }, token));
                    Assert.Equal(WorkflowInstanceStatus.CancellationRequested, (await events.LoadCheckpointAsync(owner, token)).Value.Status);
                    Assert.Equal(baseline, (await governance.LoadLatestAsync(partition, token)).Version);
                }
                else
                {
                    var release = await pools.ReleaseAsync(new ResourcePoolReleaseRequest(blocker, "blocker", DateTimeOffset.UtcNow), token);
                    Assert.Equal(held.TicketId, Assert.Single(release.ReleasedTickets).TicketId);
                    Assert.Equal(owner, Assert.Single(release.GrantedWaiters).HolderInstanceId);
                    var beforeResize = (await pools.GetPoolAsync("slot", token)).Value;
                    transferred = Assert.Single(beforeResize.HeldTickets);
                    Assert.Equal(owner, transferred.HolderInstanceId);
                    Assert.Equal(follower, Assert.Single(beforeResize.QueuedWaiters).HolderInstanceId);
                    Assert.Equal(baseline + 1, (await governance.LoadLatestAsync(partition, token)).Version);
                }
            }
        };
        await using var competing = BuildHost(services().AddSingleton<IWorkflowEventStore>(resizeReader), partition);
        var resizer = competing.GetRequiredService<IDurableResourcePoolManagement>();
        var applied = Assert.IsType<DurableResourcePoolResizeResult.Applied>(
            await resizer.ResizeAsync(slot, 2, ResourcePoolOperationId.Create("race"), ct));
        Assert.Equal(2, applied.Snapshot.ReservedUnits);
        var final = (await pools.GetPoolAsync("slot", ct)).Value;
        final.HeldTickets.Should().HaveCount(2);
        var successor = Assert.Single(final.HeldTickets, t => t.HolderInstanceId.Equals(follower));
        if (schedule == ResizeRace.OwnerCancels)
        {
            Assert.Equal(2, reads);
            Assert.Equal(1, applied.Snapshot.QueuedRequestCount);
            Assert.Equal(queued.QueuedWaiters[0].WaiterId, Assert.Single(final.QueuedWaiters).WaiterId);
            Assert.Equal(held.TicketId, Assert.Single(final.HeldTickets, t => t.HolderInstanceId.Equals(blocker)).TicketId);
            Assert.Equal(held.ProviderGeneration + 1, successor.ProviderGeneration);
            Assert.Equal(baseline + 1, (await governance.LoadLatestAsync(partition, ct)).Version);
        }
        else
        {
            Assert.Equal(1, reads);
            Assert.Equal(0, applied.Snapshot.QueuedRequestCount);
            Assert.Empty(final.QueuedWaiters);
            Assert.NotNull(transferred);
            var retained = Assert.Single(final.HeldTickets, t => t.HolderInstanceId.Equals(owner));
            Assert.Equal(transferred.TicketId, retained.TicketId);
            Assert.Equal(transferred.ProviderGeneration, retained.ProviderGeneration);
            Assert.NotEqual(retained.TicketId, successor.TicketId);
            Assert.Equal(retained.ProviderGeneration + 1, successor.ProviderGeneration);
            Assert.Equal(baseline + 2, (await governance.LoadLatestAsync(partition, ct)).Version);
        }
    }
}
