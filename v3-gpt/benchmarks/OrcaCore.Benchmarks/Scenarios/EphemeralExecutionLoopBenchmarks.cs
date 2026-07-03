using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class EphemeralExecutionLoopBenchmarks
{
    private EphemeralWorkflowEngine engine = null!;
    private WorkflowDefinition<EphemeralBenchmarkState> definition = null!;

    [Params(1, 10, 50)]
    public int StepCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        engine = new EphemeralWorkflowEngine();
        definition = EphemeralWorkflowFixture.BuildDefinition(StepCount);
        engine.RegisterDefinition(definition);
    }

    [Benchmark]
    public async Task<WorkflowInstanceSnapshot> StartAndComplete()
    {
        return await engine
            .StartAsync<int, EphemeralBenchmarkState>(definition.DefinitionId, 0, CancellationToken.None)
            .ConfigureAwait(false);
    }
}
