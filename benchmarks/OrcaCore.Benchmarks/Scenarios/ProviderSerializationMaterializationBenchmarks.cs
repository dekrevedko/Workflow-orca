using BenchmarkDotNet.Attributes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Benchmarks.Fixtures;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Benchmarks.Scenarios;

[MemoryDiagnoser]
public class ProviderSerializationMaterializationBenchmarks
{
    private readonly InMemoryWorkflowProvider provider = new();
    private readonly IWorkflowPayloadSerializer serializer = new JsonWorkflowPayloadSerializer();
    private readonly DefinitionId definitionId = DeterministicIds.Definition(60_001);
    private readonly InstanceId instanceId = DeterministicIds.Instance(60_002);
    private SerializedPayload serializedPayload = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        serializedPayload = serializer.Serialize(ProviderBenchmarkFixtures.Payload(1));
        var streamId = new WorkflowStreamId(instanceId);
        var events = Enumerable.Range(0, 128)
            .Select(index => ProviderBenchmarkFixtures.StartedEvent(index, instanceId, definitionId))
            .ToArray();
        await provider.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = events
            },
            CancellationToken.None).ConfigureAwait(false);
    }

    [Benchmark]
    public BenchmarkPayload SerializeAndDeserializePayload()
    {
        var serialized = serializer.Serialize(ProviderBenchmarkFixtures.Payload(2));
        return serializer.Deserialize<BenchmarkPayload>(serialized);
    }

    [Benchmark]
    public async Task<IReadOnlyList<WorkflowEvent>> LoadTailMaterializesEvents()
    {
        return await provider
            .LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, CancellationToken.None)
            .ConfigureAwait(false);
    }

    [Benchmark]
    public BenchmarkPayload DeserializeExistingPayload()
    {
        return serializer.Deserialize<BenchmarkPayload>(serializedPayload);
    }
}
