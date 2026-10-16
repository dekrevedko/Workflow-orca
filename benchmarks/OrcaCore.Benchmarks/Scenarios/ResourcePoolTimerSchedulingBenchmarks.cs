using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ResourcePoolTimerSchedulingBenchmarks
{
    private ServiceProvider services = null!;
    private IResourcePoolStore resourcePools = null!;
    private ITimerScheduler timers = null!;
    private readonly DateTimeOffset now = ProviderBenchmarkFixtures.StartedAt;
    private int sequence;

    [IterationSetup]
    public async Task SetupAsync()
    {
        services = ProviderBenchmarkFixtures.CreateServices();
        resourcePools = services.GetRequiredService<IResourcePoolStore>();
        timers = services.GetRequiredService<ITimerScheduler>();
        await resourcePools
            .UpsertPoolAsync(new ResourcePoolDefinition("benchmark", 128, TimeSpan.FromMinutes(5)), CancellationToken.None)
            .ConfigureAwait(false);
    }

    [IterationCleanup]
    public void Cleanup() => services.Dispose();

    [Benchmark]
    public async Task<ResourcePoolReleaseResult> AcquireAndReleasePoolTicket()
    {
        var current = Interlocked.Increment(ref sequence);
        var instanceId = DeterministicIds.Instance(80_000 + current);
        var holderKey = $"step-{current}";
        await resourcePools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                instanceId,
                holderKey,
                [new ResourcePoolRequirement("benchmark", 1)],
                now,
                now.AddMinutes(5)),
            CancellationToken.None).ConfigureAwait(false);
        return await resourcePools
            .ReleaseAsync(new ResourcePoolReleaseRequest(instanceId, holderKey, now), CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<IReadOnlyList<FireTimerCommand>> ScheduleAndClaimDueTimer()
    {
        var current = Interlocked.Increment(ref sequence);
        await timers.ScheduleAsync(
            new TimerScheduleRequest
            {
                TimerId = DeterministicIds.Timer(90_000 + current),
                InstanceId = DeterministicIds.Instance(90_000 + current),
                CommandId = DeterministicIds.Command(90_000 + current),
                FireAt = now,
                WakeupName = "benchmark"
            },
            CancellationToken.None).ConfigureAwait(false);

        return await timers.ClaimDueAsync(now, 1, CancellationToken.None).ConfigureAwait(false);
    }
}
