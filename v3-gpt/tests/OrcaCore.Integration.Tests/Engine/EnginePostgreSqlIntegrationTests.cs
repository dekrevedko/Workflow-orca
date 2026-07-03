using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Engine;

[Collection(nameof(PostgreSqlCollection))]
[Trait(Traits.Category, Traits.Integration)]
[Trait(Traits.Container, "PostgreSql")]
public sealed class EnginePostgreSqlIntegrationTests(PostgreSqlOrcaFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-EP-001")]
    [Trait("AC", "AC-301")]
    public async Task INT_EP_001_WaitSurvivesProcessorRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        var restarted = await fixture.CreateProcessorAsync(store);
        var result = await restarted.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-003")]
    [Trait("AC", "AC-309")]
    public async Task INT_EP_003_ConcurrentResumeSerializes()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        var first = processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var second = processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);

        results.Count(r => r.Outcome == DurableCommandOutcome.Committed).Should().Be(1);
        results.Count(r => r.Outcome is DurableCommandOutcome.NoOp or DurableCommandOutcome.Conflict).Should().Be(1);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-005")]
    [Trait("AC", "AC-104")]
    public async Task INT_EP_005_EarlyEventBufferedAndMatchedOnWaitRegistration()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 3),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle();
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.MatchedEventId.Should().Be(IntegrationIds.Event(50));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-009")]
    [Trait("AC", "AC-309")]
    public async Task INT_EP_009_StaleAppendReturnsConflict()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var streamId = new WorkflowStreamId(IntegrationIds.Instance(1));
        var first = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStartedEvent
                    {
                        EventId = IntegrationIds.Event(1),
                        InstanceId = IntegrationIds.Instance(1),
                        CommandId = IntegrationIds.Command(1),
                        CausationId = IntegrationIds.Causation(1),
                        OccurredAt = IntegrationIds.Timestamp(1),
                        DefinitionId = IntegrationIds.Definition(1),
                        DefinitionVersion = DefinitionVersion.Initial
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        var stale = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStepCompletedEvent
                    {
                        EventId = IntegrationIds.Event(2),
                        InstanceId = IntegrationIds.Instance(1),
                        CommandId = IntegrationIds.Command(2),
                        CausationId = IntegrationIds.Causation(2),
                        OccurredAt = IntegrationIds.Timestamp(2),
                        StepPath = "root/1"
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        stale.IsFailure.Should().BeTrue();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-011")]
    [Trait("AC", "AC-518")]
    public async Task INT_EP_011_ResourcePoolAcquireAndReleaseOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = await fixture.CreateProcessorAsync(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Acquire(1, 2, "holder", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Complete(1, 3),
            TestContext.Current.CancellationToken);

        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-004")]
    [Trait("AC", "AC-311")]
    public async Task INT_EP_004_StartOrGetSurvivesProcessorRestart()
    {
        Assert.Skip("PostgreSqlWorkflowStore does not implement IWorkflowStartIdempotencyStore yet.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-015")]
    [Trait("AC", "AC-308")]
    public async Task INT_EP_015_ActiveWaitQueryableFromProjection()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var management = fixture.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        var waits = await management.Instance(IntegrationIds.Instance(1))
            .GetActiveWaitsAsync(TestContext.Current.CancellationToken);

        waits.Should().ContainSingle()
            .Which.EventName.Should().Be("Approved");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-013")]
    public async Task INT_EP_013_SqlServerEnginePath_BlockedUntilRealStore()
    {
        await Task.CompletedTask;
        Assert.Skip("SqlServerWorkflowStore is an in-memory stub until real SQL I/O lands (R5 P0).");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-002")]
    [Trait("AC", "AC-114")]
    public async Task INT_EP_002_CrashBeforeCommit_BlockedWithoutInjectHook()
    {
        await Task.CompletedTask;
        Assert.Skip("PostgreSqlWorkflowStore does not expose commit-failure injection yet.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-006")]
    [Trait("AC", "AC-310")]
    public async Task INT_EP_006_OutboxPublishAfterCommitOnly()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        var processor = await fixture.CreateProcessorAsync(store, pools);
        var pump = new DurableOutboxPump(store, dispatcher);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        dispatcher.Records.Should().BeEmpty();
        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        dispatcher.Records.Should().ContainSingle(record => record.Kind == "external-job-start");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-007")]
    [Trait("AC", "DU-013")]
    public async Task INT_EP_007_CheckpointPlusTailRehydrates()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 3),
            TestContext.Current.CancellationToken);
        var checkpoint = await store.LoadCheckpointAsync(IntegrationIds.Instance(1), TestContext.Current.CancellationToken);

        var restarted = await fixture.CreateProcessorAsync(store);
        var result = await restarted.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);

        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.StreamVersion.Should().Be(new StreamVersion(2));
        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-008")]
    [Trait("AC", "AC-313")]
    public async Task INT_EP_008_ContinueAsNewOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var management = fixture.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ContinueAsNew(1, 2),
            TestContext.Current.CancellationToken);

        var snapshot = await management.Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);
        snapshot.ContinueAsNewGeneration.Should().Be(1);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-010")]
    [Trait("AC", "AC-513")]
    public async Task INT_EP_010_PauseResumeBufferedDeliveriesOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Pause(1, 3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(IntegrationCommands.Resume(1, 5), TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-012")]
    [Trait("AC", "AC-314")]
    public async Task INT_EP_012_PurgeRemovesTimersFromPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var management = fixture.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ScheduleTimer(1, 10, 2, IntegrationIds.Timestamp(5)),
            TestContext.Current.CancellationToken);
        await management.TerminateAsync(
            IntegrationIds.Instance(1),
            IntegrationIds.Timestamp(3),
            OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);
        await management.PurgeAsync(
            new RetentionPolicy
            {
                InstanceId = IntegrationIds.Instance(1),
                RequestedAt = IntegrationIds.Timestamp(4),
                Reason = "purge"
            },
            OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);

        var due = await store.ClaimDueAsync(IntegrationIds.Timestamp(10), 10, TestContext.Current.CancellationToken);
        due.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-014")]
    [Trait("AC", "AC-305")]
    public async Task INT_EP_014_InboxDedupAcrossParallelProcessors()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processorA = await fixture.CreateProcessorAsync(store);
        var processorB = await fixture.CreateProcessorAsync(store);
        await processorA.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);

        var duplicate = await processorB.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);
        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-016")]
    [Trait("AC", "AC-312")]
    public async Task INT_EP_016_HistoryPressureMetrics_BlockedUntilApiExists()
    {
        await Task.CompletedTask;
        Assert.Skip("GetPressureMetrics API not exposed on PostgreSqlWorkflowStore yet.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-017")]
    [Trait("AC", "NF-040")]
    public async Task INT_EP_017_DeserializePayloadRoundTripThroughPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.CreateStoreAsync();
        var processor = await fixture.CreateProcessorAsync(store);
        var management = fixture.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2, "root/1"),
            TestContext.Current.CancellationToken);

        var history = await management.GetHistoryAsync(
            IntegrationIds.Instance(1),
            TestContext.Current.CancellationToken);
        history.OfType<WorkflowStepCompletedEvent>().Should().ContainSingle()
            .Which.StepPath.Should().Be("root/1");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-EP-018")]
    [Trait("AC", "AC-304")]
    public async Task INT_EP_018_WaitLongColdEviction_BlockedUntilActivationLayer()
    {
        await Task.CompletedTask;
        Assert.Skip("Durable activation/eviction layer not integrated in host yet.");
    }
}
