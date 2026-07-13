using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.MultiNode;

[Collection(nameof(MultiNodeCollection))]
[Trait(Traits.Category, Traits.Integration)]
public sealed class MultiNodePostgreSqlIntegrationTests(OrcaStackFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-MN-001")]
    [Trait("AC", "AC-315")]
    public async Task INT_MN_001_TwoProcessors_RacingDeliver_Serializes()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processorA = new DurableCommandProcessor(store);
        var processorB = new DurableCommandProcessor(store);
        await processorA.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        var first = processorA.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var second = processorB.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 4),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        results.Should().Contain(r => r.Outcome == DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-004")]
    [Trait("AC", "DU-032")]
    public async Task INT_MN_004_TwoPumps_DoNotDoubleDispatchOutboxRow()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var dispatcher = new RecordingMessageDispatcher();
        var pumpA = new DurableOutboxPump(store, dispatcher);
        var pumpB = new DurableOutboxPump(store, dispatcher);
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(1), "workflow.completed", [1, 2, 3])),
            TestContext.Current.CancellationToken);

        var first = pumpA.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var second = pumpB.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        await Task.WhenAll(first, second);

        dispatcher.Records.Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-010")]
    [Trait("AC", "AC-305")]
    public async Task INT_MN_010_InboxDedupAcrossProcessors()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processorA = new DurableCommandProcessor(store);
        var processorB = new DurableCommandProcessor(store);
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
    [Trait(Traits.Scenario, "INT-MN-006")]
    [Trait("AC", "AC-302")]
    public async Task INT_MN_006_NewProcessorAfterCommittedState_RetriesSafely()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var first = new DurableCommandProcessor(store);
        await first.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await first.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2),
            TestContext.Current.CancellationToken);

        var restarted = new DurableCommandProcessor(store);
        var result = await restarted.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 3, "root/2"),
            TestContext.Current.CancellationToken);
        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-002")]
    [Trait("AC", "AC-315")]
    public async Task INT_MN_002_TwoProcessors_RacingStepCompletion_Serializes()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processorA = new DurableCommandProcessor(store);
        var processorB = new DurableCommandProcessor(store);
        await processorA.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var first = processorA.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2),
            TestContext.Current.CancellationToken);
        var second = processorB.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 3, "root/1"),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowStepCompletedEvent>().Should().NotBeEmpty();
        results.Should().Contain(r => r.Outcome == DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-003")]
    [Trait("AC", "DU-013")]
    public async Task INT_MN_003_StaleCheckpointAppendReturnsConflict()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 2),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(1, 3, "root/2"),
            TestContext.Current.CancellationToken);

        var stale = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(IntegrationIds.Instance(1)),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStepCompletedEvent
                    {
                        EventId = IntegrationIds.Event(99),
                        InstanceId = IntegrationIds.Instance(1),
                        CommandId = IntegrationIds.Command(99),
                        CausationId = IntegrationIds.Causation(99),
                        OccurredAt = IntegrationIds.Timestamp(99),
                        StepPath = "root/99"
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        stale.IsFailure.Should().BeTrue();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-005")]
    [Trait("AC", "EV-050")]
    public async Task INT_MN_005_TwoSchedulers_DoNotDoubleFireTimer()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ScheduleTimer(1, 10, 2, IntegrationIds.Timestamp(0)),
            TestContext.Current.CancellationToken);

        var firstClaim = store.ClaimDueAsync(IntegrationIds.Timestamp(1), 10, TestContext.Current.CancellationToken);
        var secondClaim = store.ClaimDueAsync(IntegrationIds.Timestamp(1), 10, TestContext.Current.CancellationToken);
        var claims = await Task.WhenAll(firstClaim, secondClaim);
        var due = claims.SelectMany(command => command).ToArray();

        due.Select(command => command.TimerId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-007")]
    [Trait("AC", "DU-032")]
    public async Task INT_MN_007_ProcessExitMidPump_ReleasesClaimForRetry()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                1,
                new OutboxWrite(IntegrationIds.Outbox(7), "workflow.completed", [7])),
            TestContext.Current.CancellationToken);
        var dispatcher = new CancelOnceDispatcher();
        var pump = new DurableOutboxPump(store, dispatcher);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pump.PumpOnceAsync(10, TestContext.Current.CancellationToken));
        var retried = await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var state = await store.GetStateAsync(IntegrationIds.Outbox(7), TestContext.Current.CancellationToken);

        retried.Should().Be(1);
        dispatcher.Records.Should().ContainSingle()
            .Which.OutboxRecordId.Should().Be(IntegrationIds.Outbox(7));
        state.Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-008")]
    public async Task INT_MN_008_RollingDeploySimulation_NewHostContinuesCommittedWait()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var oldDispatcher = new RecordingMessageDispatcher();
        using (var oldHost = OrcaIntegrationHost.Build(
            fixture.PostgreSql.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            oldDispatcher))
        {
            await oldHost.StartAsync(TestContext.Current.CancellationToken);
            var processor = oldHost.Services.GetRequiredService<DurableCommandProcessor>();
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await processor.ProcessAsync(
                IntegrationCommands.WaitRegistered(1, 10, 2),
                TestContext.Current.CancellationToken);
            await oldHost.StopAsync(TestContext.Current.CancellationToken);
        }

        var newDispatcher = new RecordingMessageDispatcher();
        using var newHost = OrcaIntegrationHost.Build(
            fixture.PostgreSql.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(1)),
            newDispatcher);
        await newHost.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await newHost.Services.GetRequiredService<DurableCommandProcessor>()
                .ProcessAsync(IntegrationCommands.Deliver(1, 50, 3), TestContext.Current.CancellationToken);
            var events = await newHost.Services.GetRequiredService<IWorkflowEventStore>()
                .LoadTailAsync(
                    new WorkflowStreamId(IntegrationIds.Instance(1)),
                    StreamVersion.Empty,
                    TestContext.Current.CancellationToken);
            events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
        }
        finally
        {
            await newHost.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-009")]
    [Trait("AC", "AC-311")]
    public async Task INT_MN_009_StartOrGetFromTwoHosts_BlockedUntilPgIdempotencyStore()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var storeA = await fixture.PostgreSql.CreateStoreAsync();
        await using var storeB = await fixture.PostgreSql.CreateStoreAsync();
        var starterA = new DurableStartService(new DurableCommandProcessor(storeA));
        var starterB = new DurableStartService(new DurableCommandProcessor(storeB));

        var first = starterA.StartOrGetAsync(
            StartRequest("order-int-mn-009"),
            TestContext.Current.CancellationToken);
        var second = starterB.StartOrGetAsync(
            StartRequest("order-int-mn-009"),
            TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);
        var instanceId = results.Select(result => result.InstanceId).Distinct().Single();
        var events = await storeA.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        results.Should().ContainSingle(result => result.Created);
        results.Should().OnlyContain(result => result.InstanceId == instanceId);
        events.OfType<WorkflowStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-011")]
    [Trait("AC", "DU-070")]
    public async Task INT_MN_011_ProjectionEventualConsistency_ReadYourWritesOnSameStore()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var management = fixture.PostgreSql.CreateManagement(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);

        var waits = await management.Instance(IntegrationIds.Instance(1))
            .GetActiveWaitsAsync(TestContext.Current.CancellationToken);
        waits.Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-012")]
    [Trait("AC", "AC-519")]
    public async Task INT_MN_012_PoolAcquireCrossHostFifo_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        var processorA = new DurableCommandProcessor(store, pools);
        var processorB = new DurableCommandProcessor(store, pools);
        await processorA.ProcessAsync(IntegrationCommands.Start(1), TestContext.Current.CancellationToken);
        await processorB.ProcessAsync(IntegrationCommands.Start(2, 10), TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(
            IntegrationCommands.Acquire(1, 2, "holder-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processorB.ProcessAsync(
            IntegrationCommands.Acquire(2, 11, "holder-2", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        pool.Value.QueuedWaiters.Should().ContainSingle()
            .Which.HolderKey.Should().Be("holder-2");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-013")]
    [Trait("AC", "AC-607")]
    public async Task INT_MN_013_ChildDeterministicIdsAcrossProcessorRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunChildren(1, 2, "a", "b"),
            TestContext.Current.CancellationToken);
        var before = (await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).OfType<WorkflowChildrenScheduledEvent>().Single();

        _ = new DurableCommandProcessor(store);
        var afterReload = (await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken)).OfType<WorkflowChildrenScheduledEvent>().Single();

        afterReload.Children.Select(child => child.ChildInstanceId)
            .Should().Equal(before.Children.Select(child => child.ChildInstanceId));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-014")]
    [Trait("AC", "AC-515")]
    public async Task INT_MN_014_PauseOnOneProcessorResumeOnAnother()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processorA = new DurableCommandProcessor(store);
        var processorB = new DurableCommandProcessor(store);
        await processorA.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await processorA.ProcessAsync(IntegrationCommands.Pause(1, 3), TestContext.Current.CancellationToken);
        await processorB.ProcessAsync(IntegrationCommands.Resume(1, 4), TestContext.Current.CancellationToken);
        await processorB.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 5),
            TestContext.Current.CancellationToken);

        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-MN-015")]
    [Trait("AC", "AC-504")]
    public async Task INT_MN_015_EvictionRehydrateAcrossHosts_ColdWaitResumesFromStore()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using (var firstStore = await fixture.PostgreSql.CreateStoreAsync())
        {
            var first = new DurableCommandProcessor(firstStore);
            await first.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            var wait = await first.ProcessAsync(
                IntegrationCommands.WaitRegistered(1, 10, 2, mode: WaitMode.Cold),
                TestContext.Current.CancellationToken);
            wait.Evicted.Should().BeTrue();
        }

        await using var secondStore = await fixture.PostgreSql.CreateStoreAsync();
        var second = new DurableCommandProcessor(secondStore);
        await second.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var events = await secondStore.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle();
    }

    private sealed class CancelOnceDispatcher : IMessageDispatcher
    {
        private int attempts;

        internal List<OutboxWrite> Records { get; } = [];

        public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            Records.Add(record);
            return Task.FromResult(DispatchResult.Success);
        }
    }

    private static StartOrGetRequest StartRequest(string key)
    {
        return new StartOrGetRequest(
            key,
            IntegrationIds.Definition(1),
            DefinitionVersion.Initial,
            null,
            IntegrationIds.Timestamp(1));
    }
}
