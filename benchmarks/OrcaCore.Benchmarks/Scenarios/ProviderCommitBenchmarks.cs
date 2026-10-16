using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ProviderCommitBenchmarks
{
    private readonly DefinitionId definitionId = DeterministicIds.Definition(100_001);
    private ServiceProvider services = null!;
    private IWorkflowEventStore provider = null!;
    private readonly WorkflowStreamId streamId = new(DeterministicIds.Instance(100_002));
    private long version;
    private int sequence;

    [IterationSetup]
    public void Setup()
    {
        services = ProviderBenchmarkFixtures.CreateServices();
        provider = services.GetRequiredService<IWorkflowEventStore>();
        version = 0;
        sequence = 0;
    }

    [IterationCleanup]
    public void Cleanup() => services.Dispose();

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
