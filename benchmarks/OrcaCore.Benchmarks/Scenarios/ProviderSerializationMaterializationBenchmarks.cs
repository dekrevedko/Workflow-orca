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
        var envelope = CreateEnvelope();
        await provider.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = events,
                Checkpoint = new CheckpointWrite(
                    instanceId,
                    new StreamVersion(events.Length),
                    DurableExecutionEnvelopeV2.ContentType,
                    envelope.Serialize())
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

    [Benchmark]
    public async Task<DurableExecutionEnvelopeV2> LoadCheckpointAndMaterializeEnvelope()
    {
        var checkpoint = await provider
            .LoadCheckpointAsync(instanceId, CancellationToken.None)
            .ConfigureAwait(false);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private DurableExecutionEnvelopeV2 CreateEnvelope()
    {
        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = instanceId,
            ContinueAsNewGeneration = 0,
            RootFiberId = "benchmark-root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 1,
                PlanFingerprint = "provider-materialization-benchmark"
            },
            StateContentType = serializedPayload.ContentType,
            StatePayload = serializedPayload.Payload,
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "benchmark-root",
                    InstructionId = "root/1",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0
                }
            ],
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["benchmark-root"],
                NextFiberId = "benchmark-root"
            }
        };
    }
}
