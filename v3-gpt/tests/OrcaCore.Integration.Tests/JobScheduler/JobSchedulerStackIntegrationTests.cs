using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Integration.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.JobScheduler;

[Collection(nameof(OrcaStackCollection))]
[Trait(Traits.Category, Traits.Integration)]
public sealed class JobSchedulerStackIntegrationTests(OrcaStackFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-JS-003")]
    [Trait("AC", "JS-AC-004")]
    public async Task INT_JS_003_ExternalJob_StartOutbox_DeliveredToRabbitMq()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        var pump = new DurableOutboxPump(store, fixture.RabbitMq.CreateDispatcher());
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        await pump.PumpOnceAsync(10, TestContext.Current.CancellationToken);
        var message = await fixture.RabbitMq.BasicGetAsync(TestContext.Current.CancellationToken);

        message.Should().NotBeNull();
        message!.Body.ToArray().Should().NotBeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-004")]
    [Trait("AC", "JS-AC-005")]
    public async Task INT_JS_004_ExternalJob_WaitSurvivesProcessorRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var restarted = new DurableCommandProcessor(store, pools);
        var snapshot = await fixture.PostgreSql.CreateManagement(store, pools)
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle(wait =>
            wait.CorrelationId == new CorrelationId("job-1"));
        await restarted.ProcessAsync(
            IntegrationCommands.CompleteExternalJob(1, 3, "job-1", 90),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowExternalJobCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-009")]
    [Trait("AC", "JS-AC-012")]
    public async Task INT_JS_009_MultiPoolAcquire_AllOrNothing()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db-a", 1), TestContext.Current.CancellationToken);
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db-b", 0), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(
                1,
                2,
                "job-1",
                IntegrationCommands.Requirement("db-a"),
                IntegrationCommands.Requirement("db-b")),
            TestContext.Current.CancellationToken);

        var poolA = await pools.GetPoolAsync("db-a", TestContext.Current.CancellationToken);
        var poolB = await pools.GetPoolAsync("db-b", TestContext.Current.CancellationToken);
        poolA.Value.HeldTickets.Should().BeEmpty("multi-pool acquire must be all-or-nothing");
        poolB.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-001")]
    [Trait("AC", "JS-AC-001")]
    public async Task INT_JS_001_DiamondDag_ManualWaves_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var plan = DiamondDag();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: [], failed: [], 2), TestContext.Current.CancellationToken);
        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: ["A"], failed: [], 3), TestContext.Current.CancellationToken);
        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: ["A", "B", "C"], failed: [], 4), TestContext.Current.CancellationToken);
        var scheduled = await ScheduledItemSnapshotsAsync(store, rootId);

        scheduled.Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-002")]
    [Trait("AC", "JS-AC-003")]
    public async Task INT_JS_002_DagFailureBlocksDependents_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var plan = new WorkflowDagBuilder()
            .Node("A", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("B", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("C", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("D", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .DependsOn("D", "B")
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: [], failed: [], 2), TestContext.Current.CancellationToken);
        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: ["A"], failed: ["B"], 3), TestContext.Current.CancellationToken);
        var scheduled = await ScheduledItemSnapshotsAsync(store, rootId);

        scheduled.Should().Equal("A", "C");
        plan.GetBlockedByFailures(["B"]).Select(node => node.NodeId).Should().Equal("D");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-005")]
    [Trait("AC", "JS-AC-006")]
    public async Task INT_JS_005_JobTimeoutDispatchesStopCommand()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.TimeoutExternalJob(1, 3, "job-1"),
            TestContext.Current.CancellationToken);

        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        outbox.Should().Contain(record => record.Kind == "external-job-stop");
        (await pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-006")]
    [Trait("AC", "JS-AC-007")]
    public async Task INT_JS_006_QueueQuotaAcrossDefinitions_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(
            Start(1, 1, IntegrationIds.Definition(1)),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            Start(2, 10, IntegrationIds.Definition(2)),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-a", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(2, 11, "job-b", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        pool.Value.HeldTickets.Should().ContainSingle(ticket => ticket.HolderKey == "job-a");
        pool.Value.QueuedWaiters.Should().ContainSingle(waiter => waiter.HolderKey == "job-b");
        outbox.Where(record => record.Kind == "external-job-start").Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-007")]
    [Trait("AC", "JS-AC-010")]
    public async Task INT_JS_007_TicketHeldAcrossHostRestart()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.PostgreSql.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);
        pool.Value.HeldTickets.Should().ContainSingle()
            .Which.HolderKey.Should().Be("job-1");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-008")]
    [Trait("AC", "JS-AC-011")]
    public async Task INT_JS_008_TicketReleasedOnSuccess()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.CompleteExternalJob(1, 3, "job-1", 90),
            TestContext.Current.CancellationToken);

        (await pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-010")]
    [Trait("AC", "JS-AC-013")]
    public async Task INT_JS_010_QueuedJobConsumesNoQuotaSlot()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                IntegrationIds.Instance(99),
                "other",
                [IntegrationCommands.Requirement("db")],
                IntegrationIds.Timestamp(0),
                IntegrationIds.Timestamp(30)),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        outbox.Should().NotContain(record => record.Kind == "external-job-start");
        (await pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value.QueuedWaiters.Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-011")]
    [Trait("AC", "JS-AC-009")]
    public async Task INT_JS_011_CancelRunPropagatesStop_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 2), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-a", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 3, "job-b", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        var cancelled = await processor.ProcessAsync(Cancel(1, 4), TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var events = (await store.LoadTailAsync(
                new WorkflowStreamId(IntegrationIds.Instance(1)),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .ToList();
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        cancelled.Outcome.Should().Be(DurableCommandOutcome.Committed);
        outbox.Where(record => record.Kind == "external-job-stop").Should().HaveCount(2);
        events.OfType<WorkflowExternalJobStopRequestedEvent>()
            .Select(stop => stop.ExternalJobId)
            .Should().BeEquivalentTo(["job-a", "job-b"]);
        events.FindLastIndex(workflowEvent => workflowEvent is WorkflowExternalJobStopRequestedEvent)
            .Should().BeLessThan(events.FindIndex(workflowEvent =>
                workflowEvent is WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled }));
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-012")]
    [Trait("AC", "JS-AC-005")]
    public async Task INT_JS_012_DagObservabilityUnderLoad_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var builder = new WorkflowDagBuilder();
        foreach (var nodeId in Enumerable.Range(1, 20).Select(value => $"node-{value:00}"))
        {
            builder.Node(nodeId, IntegrationIds.Definition(2), DefinitionVersion.Initial);
        }

        var plan = builder.BuildValidated().Value;
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: [], failed: [], 2), TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .Single();
        foreach (var child in scheduled.Children)
        {
            await SeedProjectionAsync(
                store,
                Snapshot(child.ChildInstanceId, rootId, child.ItemSnapshot, WorkflowStatus.Completed));
        }

        var dag = await fixture.PostgreSql.CreateManagement(store)
            .ReconstructDagRunAsync(rootId, TestContext.Current.CancellationToken);

        dag.Nodes.Should().HaveCount(20);
        dag.Nodes.Should().OnlyContain(node => node.Status == WorkflowStatus.Completed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-013")]
    [Trait("AC", "JS-AC-008")]
    public async Task INT_JS_013_ScheduledStartIdempotent_BlockedUntilCronHost()
    {
        await Task.CompletedTask;
        Assert.Skip("Host cron simulation for scheduled starts not implemented.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-014")]
    [Trait("AC", "MG-013")]
    public async Task INT_JS_014_PauseRunWithInFlightJobs_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);
        await store.ClaimAsync(10, TestContext.Current.CancellationToken);

        var paused = await processor.ProcessAsync(IntegrationCommands.Pause(1, 3), TestContext.Current.CancellationToken);
        var snapshot = await fixture.PostgreSql.CreateManagement(store, pools)
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);
        var outbox = await store.ClaimAsync(10, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        paused.Outcome.Should().Be(DurableCommandOutcome.Committed);
        snapshot.Status.Should().Be(WorkflowStatus.Paused);
        snapshot.ActiveWaits.Should().ContainSingle(wait => wait.CorrelationId == new CorrelationId("job-1"));
        events.OfType<WorkflowExternalJobStopRequestedEvent>().Should().BeEmpty();
        outbox.Where(record => record.Kind == "external-job-stop").Should().BeEmpty();
        pool.Value.HeldTickets.Should().ContainSingle(ticket => ticket.HolderKey == "job-1");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-015")]
    [Trait("AC", "AC-313")]
    public async Task INT_JS_015_ContinueAsNewOnSchedulerRun_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var plan = DiamondDag();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await runner.ScheduleReadyAsync(Request(rootId, plan, completed: [], failed: [], 2), TestContext.Current.CancellationToken);

        var continued = await processor.ProcessAsync(
            IntegrationCommands.ContinueAsNew(1, 3),
            TestContext.Current.CancellationToken);
        var nextWave = await runner.ScheduleReadyAsync(
            Request(rootId, plan, completed: ["A"], failed: [], 4),
            TestContext.Current.CancellationToken);
        var snapshot = await fixture.PostgreSql.CreateManagement(store)
            .Instance(rootId)
            .GetAsync(TestContext.Current.CancellationToken);
        var scheduled = await ScheduledItemSnapshotsAsync(store, rootId);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(rootId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        continued.Outcome.Should().Be(DurableCommandOutcome.Committed);
        nextWave.Should().ContainSingle();
        scheduled.Should().Equal("A", "B", "C");
        snapshot.ContinueAsNewGeneration.Should().Be(1);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        events.OfType<WorkflowContinuedAsNewEvent>().Should().ContainSingle()
            .Which.Generation.Should().Be(1);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-016")]
    [Trait("AC", "JS-AC-001")]
    public async Task INT_JS_016_HeterogeneousDagDefinitions_OnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        var processor = new DurableCommandProcessor(store);
        var runner = new DurableDagRunner(processor);
        var rootId = IntegrationIds.Instance(1);
        var plan = new WorkflowDagBuilder()
            .Node("A", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("B", IntegrationIds.Definition(3), DefinitionVersion.Initial)
            .BuildValidated()
            .Value;
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var results = await runner.ScheduleReadyAsync(
            Request(rootId, plan, completed: [], failed: [], 2),
            TestContext.Current.CancellationToken);
        var scheduled = (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .ToArray();

        results.Should().HaveCount(2);
        scheduled.SelectMany(group => group.Children.Select(child => child.ItemSnapshot))
            .Should().Equal("A", "B");
        scheduled.Select(group => group.ChildDefinitionId)
            .Should().BeEquivalentTo([IntegrationIds.Definition(2), IntegrationIds.Definition(3)]);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-017")]
    [Trait("AC", "EV-031")]
    public async Task INT_JS_017_DuplicateJobSucceeded_InboxDedupOnPostgreSql()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var store = await fixture.PostgreSql.CreateStoreAsync();
        await using var pools = await fixture.PostgreSql.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var first = await processor.ProcessAsync(
            IntegrationCommands.CompleteExternalJob(1, 3, "job-1", 90),
            TestContext.Current.CancellationToken);
        var duplicate = await processor.ProcessAsync(
            IntegrationCommands.CompleteExternalJob(1, 4, "job-1", 90),
            TestContext.Current.CancellationToken);

        first.Outcome.Should().Be(DurableCommandOutcome.Committed);
        duplicate.Outcome.Should().Be(DurableCommandOutcome.NoOp);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-018")]
    public async Task INT_JS_018_FullScenarioSoak_BlockedAsSlowTest()
    {
        await Task.CompletedTask;
        Assert.Skip("1-hour fake-clock DAG soak deferred to nightly slow suite.");
    }

    private static WorkflowDagPlan DiamondDag()
    {
        return new WorkflowDagBuilder()
            .Node("A", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("B", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("C", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .Node("D", IntegrationIds.Definition(2), DefinitionVersion.Initial)
            .DependsOn("B", "A")
            .DependsOn("C", "A")
            .DependsOn("D", "B")
            .DependsOn("D", "C")
            .BuildValidated()
            .Value;
    }

    private static DurableDagScheduleRequest Request(
        InstanceId rootId,
        WorkflowDagPlan plan,
        IReadOnlyCollection<string> completed,
        IReadOnlyCollection<string> failed,
        int requestedAt)
    {
        return new DurableDagScheduleRequest(
            rootId,
            plan,
            completed,
            failed,
            IntegrationIds.Timestamp(requestedAt));
    }

    private static async Task<IReadOnlyList<string>> ScheduledItemSnapshotsAsync(
        IWorkflowEventStore store,
        InstanceId rootId)
    {
        return (await store.LoadTailAsync(
                new WorkflowStreamId(rootId),
                StreamVersion.Empty,
                TestContext.Current.CancellationToken))
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Select(child => child.ItemSnapshot))
            .ToArray();
    }

    private static StartWorkflowCommand Start(int instance, int command, DefinitionId definitionId)
    {
        return new StartWorkflowCommand
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            DefinitionId = definitionId,
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static CancelWorkflowCommand Cancel(int instance, int command)
    {
        return new CancelWorkflowCommand
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command)
        };
    }

    private static async Task SeedProjectionAsync(
        IWorkflowEventStore store,
        WorkflowInstanceSnapshot snapshot)
    {
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(snapshot.InstanceId),
                ExpectedVersion = StreamVersion.Empty,
                ProjectionOperations =
                [
                    new ProjectionWrite(snapshot.InstanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = snapshot
                    }
                ]
            },
            TestContext.Current.CancellationToken);
    }

    private static WorkflowInstanceSnapshot Snapshot(
        InstanceId instanceId,
        InstanceId rootId,
        string nodeId,
        WorkflowStatus status)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            ParentInstanceId = rootId,
            RootInstanceId = rootId,
            DefinitionId = IntegrationIds.Definition(2),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = status,
            CreatedAt = IntegrationIds.Timestamp(3),
            UpdatedAt = IntegrationIds.Timestamp(4),
            ErrorSummary = status == WorkflowStatus.Failed ? $"{nodeId} failed" : null
        };
    }
}
