using BenchmarkDotNet.Attributes;

namespace OrcaCore.Benchmarks.Scenarios;

// Compiler and scheduler internals are intentionally inaccessible to applications.
// Measure structured authoring through the same public boundary consumers use.
[MemoryDiagnoser]
public class StructuredFiberExecutionBenchmarks
{
    private readonly DefinitionId definitionId = DefinitionId.Parse("00000000-0000-0000-0000-000000090002");

    [Params(2, 8, 32)]
    public int BranchCount { get; set; }

    [Benchmark]
    public EphemeralWorkflowDefinition<int, int> CompileStructuredPlan() =>
        Workflow.Ephemeral<BenchmarkState>(definitionId, DefinitionVersion.Initial)
            .Init<int>(value => new BenchmarkState(value))
            .Parallel<int>(branches =>
            {
                for (var index = 0; index < BranchCount; index++)
                {
                    var ordinal = index;
                    branches.Branch<BenchmarkState>(AuthoredBranchId.Create($"branch-{ordinal}"),
                        parent => new BenchmarkState(parent.Value.Value + ordinal),
                        branch => branch.Return(state => state.Value.Value));
                }
            })
            .WhenAll((_, results) => new BenchmarkState(results.Sum(result => result.Result)))
            .End(state => state.Value.Value)
            .Build();

    private sealed record BenchmarkState(int Value);
}
