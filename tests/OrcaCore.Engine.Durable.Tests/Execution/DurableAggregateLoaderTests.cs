using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableAggregateLoaderTests
{
    [Fact]
    public async Task LoadAsync_WithoutCheckpoint_LoadsTailAfterEmptyVersion()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Tail = [Started(instanceId)]
        };
        var loader = new DurableAggregateLoader(store);

        var aggregate = await loader.LoadAsync(instanceId, TestContext.Current.CancellationToken);

        store.LoadedCheckpointFor.Should().Be(instanceId);
        store.LoadedTailAfterVersion.Should().Be(StreamVersion.Empty);
        aggregate.InstanceId.Should().Be(instanceId);
        aggregate.Status.Should().Be(WorkflowStatus.Running);
        aggregate.StreamVersion.Should().Be(new StreamVersion(1));
    }

    [Fact]
    public async Task LoadAsync_WithCheckpoint_LoadsTailAfterCheckpointVersionAndAppliesTail()
    {
        var instanceId = InstanceIdValue(1);
        var store = new RecordingEventStore
        {
            Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(7), "application/octet-stream", [1])
            {
                DefinitionId = DefinitionIdValue(1),
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Running
            },
            Tail = [StepCompleted(instanceId, "root/2")]
        };
        var loader = new DurableAggregateLoader(store);

        var aggregate = await loader.LoadAsync(instanceId, TestContext.Current.CancellationToken);

        store.LoadedTailAfterVersion.Should().Be(new StreamVersion(7));
        aggregate.StreamVersion.Should().Be(new StreamVersion(8));
        aggregate.LastStepPath.Should().Be("root/2");
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowStepCompletedEvent StepCompleted(InstanceId instanceId, string stepPath)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = instanceId,
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = stepPath
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 22, 0, 0, TimeSpan.Zero).AddSeconds(seconds);
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class RecordingEventStore : IWorkflowEventStore
    {
        public CheckpointWrite? Checkpoint { get; init; }

        public IReadOnlyList<DurableWorkflowEvent> Tail { get; init; } = [];

        public InstanceId? LoadedCheckpointFor { get; private set; }

        public StreamVersion? LoadedTailAfterVersion { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedCheckpointFor = instanceId;
            return Task.FromResult(Checkpoint is null
                ? Option<CheckpointWrite>.None
                : Option<CheckpointWrite>.Some(Checkpoint));
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedTailAfterVersion = afterVersion;
            return Task.FromResult(Tail);
        }
    }
}
