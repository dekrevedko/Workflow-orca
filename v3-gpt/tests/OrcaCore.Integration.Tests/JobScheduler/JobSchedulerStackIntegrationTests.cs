using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
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
        Assert.Skip("Engine-integrated DAG runner not available; use DagAcceptanceTests manual waves until JS-001 runner lands.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-002")]
    [Trait("AC", "JS-AC-003")]
    public async Task INT_JS_002_DagFailureBlocksDependents_BlockedUntilRunnerExists()
    {
        await Task.CompletedTask;
        Assert.Skip("Integrated DAG failure policy requires engine DAG runner.");
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
    public async Task INT_JS_006_QueueQuotaAcrossDefinitions_BlockedUntilDagRunner()
    {
        await Task.CompletedTask;
        Assert.Skip("Integrated queue quota scenario requires DAG runner with shared pool.");
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
    public async Task INT_JS_011_CancelRunPropagatesStop_BlockedUntilDagRunner()
    {
        await Task.CompletedTask;
        Assert.Skip("Cancel run with in-flight jobs requires integrated DAG runner.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-012")]
    [Trait("AC", "JS-AC-005")]
    public async Task INT_JS_012_DagObservabilityUnderLoad_BlockedUntilRunnerExists()
    {
        await Task.CompletedTask;
        Assert.Skip("20-node DAG observability requires engine DAG runner.");
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
    public async Task INT_JS_014_PauseRunWithInFlightJobs_BlockedUntilDagRunner()
    {
        await Task.CompletedTask;
        Assert.Skip("Pause run with in-flight jobs requires integrated DAG runner.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-015")]
    [Trait("AC", "AC-313")]
    public async Task INT_JS_015_ContinueAsNewOnSchedulerRun_BlockedUntilDagRunner()
    {
        await Task.CompletedTask;
        Assert.Skip("Continue-as-new mid-DAG requires scheduler runner integration.");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-JS-016")]
    [Trait("AC", "JS-AC-001")]
    public async Task INT_JS_016_HeterogeneousDagDefinitions_BlockedUntilRunnerExists()
    {
        await Task.CompletedTask;
        Assert.Skip("Heterogeneous DAG definitions require engine DAG runner.");
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
}
