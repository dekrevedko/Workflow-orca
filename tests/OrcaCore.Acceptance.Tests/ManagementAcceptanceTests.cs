using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ManagementAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-501")]
    public async Task WhereOverSnapshots_ListsAndCountsMatchingInstances()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var query = engine.Management.All().Where(instance => instance.Status == WorkflowStatus.Waiting);

        query.List().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Waiting);
        query.Count().Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-502")]
    public async Task SameSelection_DrivesListAndSupportedCommand()
    {
        var engine = new EphemeralWorkflowEngine();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(waiting);
        var first = await engine.StartAsync<string, TestState>(
            waiting.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, TestState>(
            waiting.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);
        var selection = engine.Management.All().Where(instance => instance.Status == WorkflowStatus.Waiting);

        var beforeCommand = selection.List();
        var results = await selection.RaiseEventAsync<TestState>(
            Event("Ready", "first", "resumed"),
            TestContext.Current.CancellationToken);

        beforeCommand.Select(instance => instance.InstanceId).Should().Contain([first.InstanceId, second.InstanceId]);
        results.Should().ContainSingle()
            .Which.InstanceId.Should().Be(first.InstanceId);
        engine.Management.Instance(first.InstanceId).Get().Status.Should().Be(WorkflowStatus.Completed);
        engine.Management.Instance(second.InstanceId).Get().Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    [Trait("AC", "AC-503")]
    public async Task Statistics_GroupCountsByDefinitionAndStatus()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "done", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "wait", TestContext.Current.CancellationToken);

        var groups = engine.Management.All().Statistics().Groups;

        groups.Should().Contain(group =>
            group.DefinitionId == completed.DefinitionId &&
            group.DefinitionVersion == completed.DefinitionVersion &&
            group.Status == WorkflowStatus.Completed &&
            group.Count == 1);
        groups.Should().Contain(group =>
            group.DefinitionId == waiting.DefinitionId &&
            group.DefinitionVersion == waiting.DefinitionVersion &&
            group.Status == WorkflowStatus.Waiting &&
            group.Count == 1);
    }

    [Fact]
    [Trait("AC", "AC-312")]
    public async Task Statistics_ShowHistoryPressure()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started(instanceId)],
                Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/json", [1]),
                OutboxRecords = [new OutboxWrite(OutboxRecordId.New(), "lifecycle-event", [2])],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = new WorkflowInstanceSnapshot
                        {
                            InstanceId = instanceId,
                            DefinitionId = DefinitionIdValue(1),
                            DefinitionVersion = DefinitionVersion.Initial,
                            Status = WorkflowStatus.Running,
                            CreatedAt = Timestamp(1),
                            UpdatedAt = Timestamp(2)
                        }
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var statistics = await new DurableManagement(store).All()
            .StatisticsAsync(TestContext.Current.CancellationToken);

        statistics.Pressure.TotalStreamEvents.Should().Be(1);
        statistics.Pressure.CheckpointCount.Should().Be(1);
        statistics.Pressure.PendingOutboxCount.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-009")]
    public async Task PublicApiResults_DoNotLeakLiveInstances()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "original",
            TestContext.Current.CancellationToken);

        var state = engine.Management.Instance(started.InstanceId).GetState<TestState>();
        state.Name = "mutated";

        engine.Management.Instance(started.InstanceId).GetState<TestState>().Name.Should().Be("original");
    }

    [Fact]
    [Trait("AC", "AC-115")]
    public async Task BulkRetrieval_ReturnsMatchesWithoutPerInstanceRoundTrips()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var first = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);

        var snapshots = engine.Management.Instances([first.InstanceId, second.InstanceId]).List();

        snapshots.Select(snapshot => snapshot.InstanceId).Should().BeEquivalentTo([first.InstanceId, second.InstanceId]);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> CompletedDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> WaitingDefinition()
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => new CorrelationId(state.Name))
            .Then(() => new CapturePayloadStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(string eventName, string correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = eventName,
            CorrelationId = new CorrelationId(correlationId),
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandId.New(),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 14, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CapturePayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
