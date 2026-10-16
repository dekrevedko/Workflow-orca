using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Benchmarks.Fixtures;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ManagementProjectionBenchmarks
{
    private ServiceProvider services = null!;
    private IWorkflowProjectionStore projections = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        services = ProviderBenchmarkFixtures.CreateServices();
        projections = services.GetRequiredService<IWorkflowProjectionStore>();
        var definitionId = DeterministicIds.Definition(70_001);
        var writes = Enumerable.Range(0, 1_000)
            .Select(index => new ProjectionWrite(
                DeterministicIds.Instance(70_100 + index), ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = ProviderBenchmarkFixtures.Snapshot(
                    70_100 + index, definitionId,
                    index % 4 == 0 ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Running)
            }).ToArray();
        await projections.ApplyAsync(writes, CancellationToken.None).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => services.Dispose();

    [Benchmark]
    public Task<Option<WorkflowProjectionSnapshot>> GetProjectedInstance() =>
        projections.GetAsync(DeterministicIds.Instance(70_101), CancellationToken.None);

    [Benchmark]
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListRecoveryCandidates() =>
        projections.ListLeaseRecoveryCandidatesAsync(CancellationToken.None);
}
