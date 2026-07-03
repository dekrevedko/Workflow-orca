using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Benchmarks.Fixtures;

internal static class ProviderBenchmarkFixtures
{
    internal static readonly DateTimeOffset StartedAt = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    internal static BenchmarkPayload Payload(int sequence)
    {
        return new BenchmarkPayload(
            sequence,
            $"payload-{sequence}",
            Enumerable.Range(sequence, 16).ToArray(),
            new Dictionary<string, string>
            {
                ["tenant"] = "benchmark",
                ["kind"] = "workflow"
            });
    }

    internal static WorkflowInstanceSnapshot Snapshot(
        int sequence,
        DefinitionId definitionId,
        WorkflowStatus status = WorkflowStatus.Running)
    {
        var timestamp = StartedAt.AddSeconds(sequence);
        return new WorkflowInstanceSnapshot
        {
            InstanceId = DeterministicIds.Instance(sequence),
            DefinitionId = definitionId,
            DefinitionVersion = DefinitionVersion.Initial,
            Status = status,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    internal static WorkflowEvent StartedEvent(
        int sequence,
        InstanceId instanceId,
        DefinitionId definitionId)
    {
        return new WorkflowStartedEvent
        {
            EventId = DeterministicIds.Event(sequence),
            InstanceId = instanceId,
            CommandId = DeterministicIds.Command(sequence),
            CausationId = DeterministicIds.Causation(sequence),
            OccurredAt = StartedAt.AddTicks(sequence),
            DefinitionId = definitionId,
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    internal static ProviderCommitBatch CommitBatch(
        WorkflowStreamId streamId,
        StreamVersion expectedVersion,
        int sequence,
        DefinitionId definitionId)
    {
        var instanceId = streamId.InstanceId;
        var payload = new byte[] { 1, 3, 5, 7, (byte)(sequence % 255) };
        var snapshot = Snapshot(sequence, definitionId);
        return new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = expectedVersion,
            Events = [StartedEvent(sequence, instanceId, definitionId)],
            Checkpoint = new CheckpointWrite(instanceId, expectedVersion.Next(), "application/json", payload)
            {
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Running
            },
            InboxOperations = [new InboxWrite(DeterministicIds.Event(sequence + 10_000), InboxRecordState.Applied)],
            OutboxRecords = [new OutboxWrite(DeterministicIds.Outbox(sequence), "benchmark", payload)],
            ProjectionOperations =
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = snapshot
                }
            ]
        };
    }
}
