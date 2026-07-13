using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.TestSupport;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Providers.PostgreSql.Tests;

[Trait(Traits.Container, "PostgreSql")]
public sealed class PostgreSqlEventStoreTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("orcacore")
        .WithUsername("orcacore")
        .WithPassword("orcacore")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Append_WithExpectedVersion_CommitsEventsAndAdvancesVersion()
    {
        var store = await CreateStoreAsync();
        var streamId = new WorkflowStreamId(InstanceIdValue(1));

        var result = await store.AppendAsync(
            Batch(streamId, StreamVersion.Empty, StartEvent(streamId.InstanceId)),
            TestContext.Current.CancellationToken);
        var tail = await store.LoadTailAsync(streamId, StreamVersion.Empty, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewVersion.Should().Be(new StreamVersion(1));
        tail.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(StartEvent(streamId.InstanceId), options => options.Excluding(@event => @event.EventId));
    }

    [Fact]
    public async Task Append_ConflictingExpectedVersion_ReturnsConflict()
    {
        var store = await CreateStoreAsync();
        var streamId = new WorkflowStreamId(InstanceIdValue(1));
        await store.AppendAsync(
            Batch(streamId, StreamVersion.Empty, StartEvent(streamId.InstanceId)),
            TestContext.Current.CancellationToken);

        var result = await store.AppendAsync(
            Batch(streamId, StreamVersion.Empty, StepCompletedEvent(streamId.InstanceId)),
            TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("expected version");
    }

    [Fact]
    public async Task Checkpoint_SaveAndLoad_RoundTripsPayloadAndVersion()
    {
        var store = await CreateStoreAsync();
        var instanceId = InstanceIdValue(1);
        var checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/octet-stream", [1, 2, 3])
        {
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = new DefinitionVersion(7),
            Status = WorkflowStatus.Waiting,
            LastStepPath = "root.step",
            ErrorSummary = "none",
            OutcomeName = "done"
        };

        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [StartEvent(instanceId)],
                Checkpoint = checkpoint
            },
            TestContext.Current.CancellationToken);
        var loaded = await store.LoadCheckpointAsync(instanceId, TestContext.Current.CancellationToken);

        loaded.HasValue.Should().BeTrue();
        loaded.Value.Should().BeEquivalentTo(checkpoint);
    }

    [Fact]
    [Trait("AC", "AC-301")]
    public async Task PostgreSql_DurableWaitSurvivesRestart()
    {
        var instanceId = InstanceIdValue(1);
        await using (var firstStore = await CreateStoreAsync())
        {
            await firstStore.AppendAsync(
                Batch(
                    new WorkflowStreamId(instanceId),
                    StreamVersion.Empty,
                    StartEvent(instanceId),
                    WaitRegisteredEvent(instanceId)),
                TestContext.Current.CancellationToken);
        }

        await using var restartedStore = await CreateStoreAsync();
        var tail = await restartedStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        tail.OfType<WorkflowWaitRegisteredEvent>().Should().ContainSingle()
            .Which.WaitId.Should().Be(WaitIdValue(1));
    }

    [Fact]
    [Trait("AC", "AC-302")]
    public async Task PostgreSql_CrashRestoresCommittedStateOnly()
    {
        var instanceId = InstanceIdValue(1);
        var store = await CreateStoreAsync();
        await store.AppendAsync(
            Batch(new WorkflowStreamId(instanceId), StreamVersion.Empty, StartEvent(instanceId)),
            TestContext.Current.CancellationToken);

        await store.AppendAsync(
            Batch(new WorkflowStreamId(instanceId), StreamVersion.Empty, WaitRegisteredEvent(instanceId)),
            TestContext.Current.CancellationToken);
        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        tail.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStartedEvent>();
    }

    [Fact]
    [Trait("AC", "AC-309")]
    public async Task PostgreSql_ConcurrentResumeSerializes()
    {
        var store = await CreateStoreAsync();
        var streamId = new WorkflowStreamId(InstanceIdValue(1));
        var first = store.AppendAsync(
            Batch(streamId, StreamVersion.Empty, StartEvent(streamId.InstanceId)),
            TestContext.Current.CancellationToken);
        var second = store.AppendAsync(
            Batch(streamId, StreamVersion.Empty, StepCompletedEvent(streamId.InstanceId)),
            TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
    }

    private async Task<PostgreSqlWorkflowStore> CreateStoreAsync()
    {
        var store = new PostgreSqlWorkflowStore(container.GetConnectionString());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        return store;
    }

    private static ProviderCommitBatch Batch(
        WorkflowStreamId streamId,
        StreamVersion expectedVersion,
        params WorkflowEvent[] events)
    {
        return new ProviderCommitBatch
        {
            StreamId = streamId,
            ExpectedVersion = expectedVersion,
            Events = events
        };
    }

    private static WorkflowStartedEvent StartEvent(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowStepCompletedEvent StepCompletedEvent(InstanceId instanceId)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = "root.step"
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegisteredEvent(InstanceId instanceId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.New(),
            InstanceId = instanceId,
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            WaitId = WaitIdValue(1),
            EventName = "Approved",
            CorrelationId = new CorrelationId("order-1")
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

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
