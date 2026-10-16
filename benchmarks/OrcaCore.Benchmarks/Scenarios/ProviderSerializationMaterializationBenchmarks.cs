using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;
using DurableWorkflowEvent = OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ProviderSerializationMaterializationBenchmarks
{
    private ServiceProvider services = null!;
    private IWorkflowEventStore provider = null!;
    private readonly InstanceId instanceId = DeterministicIds.Instance(60_002);
    private byte[] serializedPayload = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        services = ProviderBenchmarkFixtures.CreateServices();
        provider = services.GetRequiredService<IWorkflowEventStore>();
        serializedPayload = JsonSerializer.SerializeToUtf8Bytes(ProviderBenchmarkFixtures.Payload(1));
        var events = Enumerable.Range(0, 128)
            .Select(index => ProviderBenchmarkFixtures.StartedEvent(
                index, instanceId, DeterministicIds.Definition(60_001))).ToArray();
        var result = await provider.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events = events,
            Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(events.Length),
                "application/json", serializedPayload)
        }, CancellationToken.None).ConfigureAwait(false);
        if (result.IsFailure) throw new InvalidOperationException(result.Error.Message);
    }

    [GlobalCleanup]
    public void Cleanup() => services.Dispose();

    // Measures application JSON, not the internal fixed workflow codec.
    [Benchmark]
    public BenchmarkPayload SerializeAndDeserializePayload() =>
        JsonSerializer.Deserialize<BenchmarkPayload>(
            JsonSerializer.SerializeToUtf8Bytes(ProviderBenchmarkFixtures.Payload(2)))!;

    [Benchmark]
    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailMaterializesEvents() =>
        provider.LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, CancellationToken.None);

    [Benchmark]
    public BenchmarkPayload DeserializeExistingPayload() =>
        JsonSerializer.Deserialize<BenchmarkPayload>(serializedPayload)!;

    [Benchmark]
    public async Task<BenchmarkPayload> LoadCheckpointAndMaterializePayload()
    {
        var checkpoint = await provider.LoadCheckpointAsync(instanceId, CancellationToken.None).ConfigureAwait(false);
        return JsonSerializer.Deserialize<BenchmarkPayload>(checkpoint.Value.Payload)!;
    }
}
