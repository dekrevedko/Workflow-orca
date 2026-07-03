using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ManagementProjectionBenchmarks
{
    private readonly InMemoryWorkflowProvider provider = new();
    private readonly DefinitionId definitionId = DeterministicIds.Definition(70_001);
    private DurableManagement management = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        management = new DurableManagement(provider);
        var projections = Enumerable.Range(0, 1_000)
            .Select(index => new ProjectionWrite(
                DeterministicIds.Instance(70_100 + index),
                ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = ProviderBenchmarkFixtures.Snapshot(
                    70_100 + index,
                    definitionId,
                    index % 4 == 0 ? WorkflowStatus.Completed : WorkflowStatus.Running)
            })
            .ToArray();
        await provider.ApplyAsync(projections, CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<int> CountRunningForDefinition()
    {
        return await management
            .ForDefinition(definitionId)
            .Where(instance => instance.Status == WorkflowStatus.Running)
            .CountAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListRunningForDefinition()
    {
        return await management
            .All()
            .Where(instance => instance.DefinitionId == definitionId && instance.Status == WorkflowStatus.Running)
            .ListAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }
}
