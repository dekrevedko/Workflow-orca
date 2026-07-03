using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ResourcePoolTimerSchedulingBenchmarks
{
    private readonly InMemoryResourcePoolStore resourcePools = new();
    private readonly InMemoryWorkflowProvider timers = new();
    private readonly DateTimeOffset now = ProviderBenchmarkFixtures.StartedAt;
    private int sequence;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        await resourcePools
            .UpsertPoolAsync(new ResourcePoolDefinition("benchmark", 128, TimeSpan.FromMinutes(5)), CancellationToken.None)
            .ConfigureAwait(false);
    }

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
