using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Hosting;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class EphemeralExecutionLoopBenchmarks
{
    private ServiceProvider services = null!;
    private EphemeralDefinitionHandle<int, int> definition = null!;
    private long sequence;

    [Params(1, 10, 50)]
    public int StepCount { get; set; }

    [IterationSetup]
    public void Setup()
    {
        var workflow = EphemeralWorkflowFixture.BuildDefinition(StepCount);
        var collection = new ServiceCollection();
        collection.AddTransient<IncrementStep>();
        collection.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        }).AddWorkflow(workflow);
        services = collection.BuildServiceProvider();
        definition = services.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(workflow.Reference);
    }

    [IterationCleanup]
    public void Cleanup() => services.Dispose();

    [Benchmark]
    public async Task<int> StartAndComplete()
    {
        var start = await definition.StartOrGetAsync(
            0, StartIdempotencyKey.Create($"benchmark-{++sequence}"), CancellationToken.None).ConfigureAwait(false);
        return await start.WaitForOutputAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
