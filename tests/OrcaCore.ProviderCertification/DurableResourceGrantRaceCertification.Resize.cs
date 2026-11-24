using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static partial class DurableResourceGrantRaceCertification
{
    public static async Task ResizeQueueAsync(Func<IServiceCollection> services, Type providerType)
    {
        var ct = TestContext.Current.CancellationToken;
        var partition = ResourceGovernancePartitionId.Create($"resize-{Guid.CreateVersion7():N}");
        var slot = ResourcePoolName.Create("slot");
        var other = DurableResourcePoolDefinition.Create(ResourcePoolName.Create("other"), 1, TimeSpan.FromHours(1));
        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(value => value)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(slot, 2)),
                scope => scope.Then<CountedStep>()).End().Build();
        var rival = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var resizeId = ResourcePoolOperationId.Create("upsize");
        InstanceId owner;
        Guid firstTicket;
        Guid secondTicket;
        await using (var host = BuildHost(services(), partition, other))
        {
            await host.GetServices<IHostedService>().Single(s => s.GetType().Assembly == providerType.Assembly).StartAsync(ct);
            var runtime = CreateRuntime(host, NullDurableResourceLeaseCertificationGate.Instance);
            runtime.RegisterDefinition(definition);
            owner = (await runtime.StartOrGetAsync<int, int>("owner", definition.DefinitionId, definition.DefinitionVersion, 0, ct)).InstanceId;
            var pools = host.GetRequiredService<IResourcePoolStore>();
            var second = new ResourcePoolAcquireRequest(rival, "rival", [new ResourcePoolRequirement("other", 1)], DateTimeOffset.UtcNow, null);
            Assert.Equal(ResourcePoolAcquireStatus.Queued, (await pools.AcquireAsync(second, ct)).Status);
            Assert.Empty((await pools.GetPoolAsync("slot", ct)).Value.HeldTickets);
            Assert.Empty((await pools.GetPoolAsync("other", ct)).Value.HeldTickets);
            Assert.Equal(owner, Assert.Single((await pools.GetPoolAsync("slot", ct)).Value.QueuedWaiters).HolderInstanceId);

            var management = host.GetRequiredService<IDurableResourcePoolManagement>();
            var applied = Assert.IsType<DurableResourcePoolResizeResult.Applied>(await management.ResizeAsync(slot, 2, resizeId, ct));
            Assert.Equal(2, applied.Snapshot.ReservedUnits);
            Assert.Equal(0, applied.Snapshot.QueuedRequestCount);
            var a = Assert.Single((await pools.GetPoolAsync("slot", ct)).Value.HeldTickets);
            var b = Assert.Single((await pools.GetPoolAsync("other", ct)).Value.HeldTickets);
            Assert.Equal(owner, a.HolderInstanceId);
            Assert.Equal(rival, b.HolderInstanceId);
            Assert.Equal(2, a.Count);
            Assert.Equal(1, b.Count);
            Assert.Empty((await pools.GetPoolAsync("other", ct)).Value.QueuedWaiters);
            firstTicket = a.TicketId;
            secondTicket = b.TicketId;
        }

        await using var replacement = BuildHost(services(), partition, other);
        var replay = replacement.GetRequiredService<IDurableResourcePoolManagement>();
        Assert.IsType<DurableResourcePoolResizeResult.Applied>(await replay.ResizeAsync(slot, 2, resizeId, ct));
        var recovered = replacement.GetRequiredService<IResourcePoolStore>();
        Assert.Equal(firstTicket, Assert.Single((await recovered.GetPoolAsync("slot", ct)).Value.HeldTickets).TicketId);
        var driver = CreateRuntime(replacement, NullDurableResourceLeaseCertificationGate.Instance);
        driver.RegisterDefinition(definition);
        await driver.DriveAsync(owner, ct);
        var events = replacement.GetRequiredService<IWorkflowEventStore>();
        var history = await events.LoadTailAsync(new WorkflowStreamId(owner), StreamVersion.Empty, ct);
        Assert.Equal(firstTicket, Assert.Single(Assert.Single(history.OfType<WorkflowResourcePoolReleasedEvent>()).Tickets).TicketId);
        Assert.Equal(1, replacement.GetRequiredService<CountedStep>().Calls);
        Assert.Equal(secondTicket, Assert.Single((await recovered.GetPoolAsync("other", ct)).Value.HeldTickets).TicketId);
    }
}
