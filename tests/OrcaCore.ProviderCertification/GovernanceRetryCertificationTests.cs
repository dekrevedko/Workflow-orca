using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class GovernanceRetryCertificationTests
{
    private const string ResizeExhausted = "Durable resource governance resize could not serialize after repeated conflicts.";
    private static readonly ResourcePoolName Target = ResourcePoolName.Create("database");
    private static readonly ResourcePoolName Rival = ResourcePoolName.Create("cache");
    private readonly ResourceGovernancePartitionId partition = ResourceGovernancePartitionId.Create($"retry-{Guid.NewGuid():N}");

    protected abstract IServiceCollection CreateProviderServices();

    [Fact]
    public async Task ExhaustedResizeLeavesNoMutationAndSameOperationCanBeRetried()
    {
        var token = TestContext.Current.CancellationToken;
        await using var otherHost = BuildHost();
        var other = otherHost.GetRequiredService<IDurableResourcePoolManagement>();
        var pools = otherHost.GetRequiredService<IResourcePoolStore>();
        var held = await pools.AcquireAsync(Request("holder"), token);
        var queued = await pools.AcquireAsync(Request("waiter"), token);
        Assert.Equal(ResourcePoolAcquireStatus.Granted, held.Status);
        Assert.Equal(ResourcePoolAcquireStatus.Queued, queued.Status);
        var before = await other.GetAsync(Target, token);
        var audit = otherHost.GetRequiredService<IDurableResourceGovernanceStore>();
        var initialVersion = (await audit.LoadLatestAsync(partition, token)).Version;

        await using var raw = CreateProviderServices().BuildServiceProvider();
        var probe = new CompetingResizeStore(raw.GetRequiredService<IDurableResourceGovernanceStore>(), other);
        await using var victimHost = BuildHost(probe);
        var victim = victimHost.GetRequiredService<IDurableResourcePoolManagement>();
        var operation = ResourcePoolOperationId.Create("victim");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => victim.ResizeAsync(Target, 2, operation, token).AsTask());
        Assert.Equal(ResizeExhausted, failure.Message);
        Assert.Equal(32, probe.Conflicts);
        Assert.Equal(initialVersion + 32, (await audit.LoadAsync(partition, token)).Version);
        Assert.Equal(before, await other.GetAsync(Target, token));
        var persisted = (await pools.GetPoolAsync(Target.Value, token)).Value;
        Assert.Equal(Assert.Single(held.Tickets), Assert.Single(persisted.HeldTickets));
        Assert.Equal(queued.QueuedWaiter!.WaiterId, Assert.Single(persisted.QueuedWaiters).WaiterId);

        await using var restarted = BuildHost();
        var recovery = restarted.GetRequiredService<IDurableResourcePoolManagement>();
        var applied = Assert.IsType<DurableResourcePoolResizeResult.Applied>(await recovery.ResizeAsync(Target, 2, operation, token));
        Assert.Equal(2, applied.Snapshot.ConfiguredCapacity);
        Assert.Equal(initialVersion + 33, (await audit.LoadLatestAsync(partition, token)).Version);
        Assert.Equal(applied, await other.ResizeAsync(Target, 2, operation, token));
        Assert.IsType<DurableResourcePoolResizeResult.Conflict>(await other.ResizeAsync(Target, 3, operation, token));
        Assert.Equal(initialVersion + 33, (await audit.LoadAsync(partition, token)).Version);
    }

    [Fact(Explicit = true)]
    public async Task MeasureConcurrentResizeBurst()
    {
        const int hosts = 6;
        const int requests = 36;
        var token = TestContext.Current.CancellationToken;
        var providers = new List<ServiceProvider>();
        try
        {
            var clients = new List<IDurableResourcePoolManagement>();
            for (var i = 0; i < hosts; i++)
            {
                var host = BuildHost();
                providers.Add(host);
                var management = host.GetRequiredService<IDurableResourcePoolManagement>();
                await management.GetAsync(Target, token);
                clients.Add(management);
            }

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = Enumerable.Range(0, requests).Select(async i =>
            {
                var operation = ResourcePoolOperationId.Create($"burst-{i}");
                var capacity = 2 + i;
                await start.Task.WaitAsync(token);
                var began = Stopwatch.GetTimestamp();
                try
                {
                    var result = await clients[i % hosts].ResizeAsync(Target, capacity, operation, token);
                    Assert.Equal(capacity, Assert.IsType<DurableResourcePoolResizeResult.Applied>(result).Snapshot.ConfiguredCapacity);
                    return (operation, capacity, applied: true, elapsed: Stopwatch.GetElapsedTime(began));
                }
                catch (InvalidOperationException e) when (e.Message == ResizeExhausted)
                {
                    return (operation, capacity, applied: false, elapsed: Stopwatch.GetElapsedTime(began));
                }
            }).ToArray();
            var began = Stopwatch.GetTimestamp();
            start.SetResult();
            var outcomes = await Task.WhenAll(calls);
            var duration = Stopwatch.GetElapsedTime(began);
            var ordered = outcomes.Select(o => o.elapsed.TotalMilliseconds).Order().ToArray();
            var committed = outcomes.Count(o => o.applied);
            var output = TestContext.Current.TestOutputHelper!;
            output.WriteLine($"hosts={hosts} requests={requests} committed={committed} exhausted={requests - committed} wall_ms={duration.TotalMilliseconds:F1} p50_ms={ordered[(requests - 1) / 2]:F1} p95_ms={ordered[(int)Math.Ceiling(requests * .95) - 1]:F1} max_ms={ordered[^1]:F1}");
            foreach (var call in outcomes)
                output.WriteLine($"{call.operation.Value} applied={call.applied} ms={call.elapsed.TotalMilliseconds:F1}");

            var audit = providers[0].GetRequiredService<IDurableResourceGovernanceStore>();
            long version = 1 + committed;
            Assert.Equal(version, (await audit.LoadAsync(partition, token)).Version);
            var final = await clients[0].GetAsync(Target, token);
            Assert.Contains(outcomes, o => o.applied && o.capacity == final.ConfiguredCapacity);
            Assert.Equal(new DurableResourcePoolSnapshot(Target, final.ConfiguredCapacity, 0, 0, 0, null), final);
            foreach (var call in outcomes)
            {
                var replay = await clients[0].ResizeAsync(Target, call.capacity, call.operation, token);
                Assert.Equal(call.capacity, Assert.IsType<DurableResourcePoolResizeResult.Applied>(replay).Capacity);
                if (!call.applied)
                    version++;
                Assert.Equal(version, (await audit.LoadLatestAsync(partition, token)).Version);
            }
        }
        finally
        {
            foreach (var host in providers)
                await host.DisposeAsync();
        }
    }

    private static ResourcePoolAcquireRequest Request(string holder) =>
        new(InstanceId.Parse(Guid.NewGuid().ToString()), holder, [new ResourcePoolRequirement(Target.Value, 1)], DateTimeOffset.UtcNow, null);

    private ServiceProvider BuildHost(IDurableResourceGovernanceStore? store = null)
    {
        var services = CreateProviderServices();
        if (store is not null)
            services.AddSingleton(store);
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = partition,
                Pools = [DurableResourcePoolDefinition.Create(Target, 1, TimeSpan.FromHours(1)),
                         DurableResourcePoolDefinition.Create(Rival, 1, TimeSpan.FromHours(1))]
            }
        });
        return services.BuildServiceProvider();
    }

    private sealed class CompetingResizeStore(IDurableResourceGovernanceStore inner, IDurableResourcePoolManagement other) : IDurableResourceGovernanceStore
    {
        public int Conflicts { get; private set; }

        public ValueTask<ResourceGovernanceHead> LoadLatestAsync(ResourceGovernancePartitionId id, CancellationToken token) =>
            inner.LoadLatestAsync(id, token);

        public ValueTask<ResourceGovernanceStream> LoadAsync(ResourceGovernancePartitionId id, CancellationToken token) =>
            inner.LoadAsync(id, token);

        public async ValueTask<ResourceGovernanceAppendResult> AppendAsync(ResourceGovernancePartitionId id, long expectedVersion,
            IReadOnlyList<ResourceGovernanceRecord> records, CancellationToken token)
        {
            if (Conflicts >= 32)
                return await inner.AppendAsync(id, expectedVersion, records, token);

            var competing = await other.ResizeAsync(Rival, Conflicts + 2, ResourcePoolOperationId.Create($"rival-{Conflicts}"), token);
            Assert.IsType<DurableResourcePoolResizeResult.Applied>(competing);
            var result = await inner.AppendAsync(id, expectedVersion, records, token);
            Assert.Equal(new ResourceGovernanceAppendResult.Conflict(expectedVersion + 1), result);
            Conflicts++;
            return result;
        }
    }
}
