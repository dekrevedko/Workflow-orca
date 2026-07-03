using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ProviderCommitBenchmarks
{
    private readonly DefinitionId definitionId = DeterministicIds.Definition(100_001);
    private readonly InMemoryWorkflowProvider provider = new();
    private readonly WorkflowStreamId streamId = new(DeterministicIds.Instance(100_002));
    private long version;
    private int sequence;

    [Benchmark]
    public async Task<AppendEventsResult> AppendCommitBatch()
    {
        var currentVersion = new StreamVersion(Interlocked.Read(ref version));
        var currentSequence = Interlocked.Increment(ref sequence);
        var batch = ProviderBenchmarkFixtures.CommitBatch(
            streamId,
            currentVersion,
            100_100 + currentSequence,
            definitionId);

        var result = await provider.AppendAsync(batch, CancellationToken.None).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        Interlocked.Exchange(ref version, result.Value.NewVersion.Value);
        return result.Value;
    }
}
