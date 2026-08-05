using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Integration.Tests.Fixtures;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.SampleHost;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Integration.Tests.Hosting;

[Collection(nameof(PostgreSqlCollection))]
[Trait(Traits.Category, Traits.Integration)]
public sealed class HostingPostgreSqlIntegrationTests(PostgreSqlOrcaFixture fixture)
{
    [Fact]
    [Trait(Traits.Scenario, "INT-HO-001")]
    [Trait("AC", "AC-001")]
    public async Task INT_HO_001_SampleHostRunsEphemeralWorkflowToCompletion()
    {
        using var host = SampleHostApplication.Build([]);
        var engine = host.Services.GetRequiredService<EphemeralWorkflowEngine>();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Value = input })
            .Then(() => new CompletedStep())
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.AwaitCompletionAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-002")]
    [Trait("AC", "AC-310")]
    public async Task INT_HO_002_HostPumpDispatchesProcessorCommittedOutbox()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        var pools = host.Services.GetRequiredService<IResourcePoolStore>();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var dispatched = await dispatcher.WaitForDispatchAsync(
                record => record.Kind == "external-job-start",
                TestContext.Current.CancellationToken);
            dispatched.Kind.Should().Be("external-job-start");
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-003")]
    [Trait("AC", "AC-111")]
    public async Task INT_HO_003_HostTimerServiceFiresDueTimer()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0));
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(fixture.ConnectionString, clock, dispatcher);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(IntegrationCommands.Start(3), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ScheduleTimer(3, 10, 2, IntegrationIds.Timestamp(0)),
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        await OrcaIntegrationHost.FireTimersOnceAsync(host, TestContext.Current.CancellationToken);

        var store = host.Services.GetRequiredService<IWorkflowEventStore>();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(3)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-007")]
    [Trait("AC", "PR-040")]
    public void INT_HO_007_PostgreSqlHostResolvesPostgreSqlStore()
    {
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);

        host.Services.GetRequiredService<IWorkflowEventStore>()
            .Should().BeOfType<OrcaCore.Providers.PostgreSql.PostgreSqlWorkflowStore>();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-013")]
    public void INT_HO_013_HostRegistersBothEngines()
    {
        using var host = SampleHostApplication.Build([]);
        host.Services.GetRequiredService<EphemeralWorkflowEngine>().Should().NotBeNull();
        host.Services.GetRequiredService<DurableCommandProcessor>().Should().NotBeNull();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-004")]
    [Trait("AC", "AC-316")]
    public async Task INT_HO_004_GracefulHostShutdownAfterPumpCycle()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var (host, dispatcher, _) = await OrcaIntegrationHost.BuildPostgreSqlAsync(
            fixture.ConnectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (host)
        {
            var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await OrcaIntegrationHost.PumpOutboxOnceAsync(host, TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
            dispatcher.Records.Should().NotBeEmpty();
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-006")]
    [Trait("AC", "AC-521")]
    public async Task INT_HO_006_OperationalSweepExpiresPoolTickets()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        await using var pools = await fixture.CreatePoolStoreAsync();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0));
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(fixture.ConnectionString, clock, dispatcher);
        await host.StartAsync(TestContext.Current.CancellationToken);
        var sweep = host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .OfType<OrcaCoreOperationalSweepHostedService>()
            .Should().ContainSingle().Subject;
        await sweep.StopAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-005")]
    [Trait("AC", "AC-316")]
    public async Task INT_HO_005_HostShutdownMidProcessorCommand_RollsBackCleanly()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var appendEntered = new TaskCompletionSource<PostgreSqlAppendContext>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAppend = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new PostgreSqlWorkflowStoreOptions
        {
            BeforeCommitAsync = async (context, cancellationToken) =>
            {
                appendEntered.TrySetResult(context);
                await releaseAppend.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        };
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher,
            configureServices: services => services.AddSingleton(options));
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        var command = processor.ProcessAsync(IntegrationCommands.Start(), commandCancellation.Token);

        var context = await appendEntered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await commandCancellation.CancelAsync();
        releaseAppend.TrySetResult();
        var act = async () => await command;

        context.EventCount.Should().Be(1);
        await act.Should().ThrowAsync<OperationCanceledException>();
        await host.StopAsync(TestContext.Current.CancellationToken);

        await using var restarted = await fixture.CreateStoreAsync();
        var events = await restarted.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var checkpoint = await restarted.LoadCheckpointAsync(
            IntegrationIds.Instance(1),
            TestContext.Current.CancellationToken);
        var outbox = await restarted.ClaimAsync(10, TestContext.Current.CancellationToken);

        events.Should().BeEmpty();
        checkpoint.HasValue.Should().BeFalse();
        outbox.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-008")]
    public void INT_HO_008_HostedServicesIdempotentRegistration()
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        services.AddOrcaCoreHostedServices();
        services.AddOrcaCoreHostedServices();
        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOutboxPumpHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreTimerHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOperationalSweepHostedService);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-009")]
    [Trait("AC", "DU-032")]
    public async Task INT_HO_009_OutboxPumpBatchSizeBoundary()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher,
            options => options.OutboxPumpBatchSize = 10);
        var store = host.Services.GetRequiredService<IWorkflowEventStore>();
        for (var i = 1; i <= 15; i++)
        {
            await store.AppendAsync(
                IntegrationCommands.OutboxOnlyBatch(
                    1,
                    new OutboxWrite(IntegrationIds.Outbox(i), "workflow.completed", [(byte)i])),
                TestContext.Current.CancellationToken);
        }

        await OrcaIntegrationHost.PumpOutboxOnceAsync(host, TestContext.Current.CancellationToken);
        await OrcaIntegrationHost.PumpOutboxOnceAsync(host, TestContext.Current.CancellationToken);

        dispatcher.Records.Should().HaveCount(15);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-010")]
    [Trait("AC", "EV-050")]
    public async Task INT_HO_010_TimerSweepWithNoDueTimers_IsNoOp()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0));
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(fixture.ConnectionString, clock, dispatcher);
        await host.StartAsync(TestContext.Current.CancellationToken);
        await OrcaIntegrationHost.FireTimersOnceAsync(host, TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
        var store = host.Services.GetRequiredService<IWorkflowEventStore>();
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(1)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        events.Should().BeEmpty();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-011")]
    [Trait("AC", "NF-020")]
    public async Task INT_HO_011_FakeTimeProviderDrivesHostedIntervals()
    {
        Assert.Skip(
            "Deferred: task 10.5 must replace this raw-command fixture with a supported public hosting journey.");
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0));
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(fixture.ConnectionString, clock, dispatcher);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(IntegrationCommands.Start(4), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.ScheduleTimer(4, 11, 2, IntegrationIds.Timestamp(0)),
            TestContext.Current.CancellationToken);
        await host.Services.GetRequiredService<IWorkflowEventStore>().AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                4,
                new OutboxWrite(IntegrationIds.Outbox(4), "workflow.completed", [1])),
            TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromMinutes(1));
        await OrcaIntegrationHost.PumpOutboxOnceAsync(host, TestContext.Current.CancellationToken);
        await OrcaIntegrationHost.FireTimersOnceAsync(host, TestContext.Current.CancellationToken);
        await OrcaIntegrationHost.RunOperationalSweepOnceAsync(host, TestContext.Current.CancellationToken);

        dispatcher.Records.Should().NotBeEmpty();
        var store = host.Services.GetRequiredService<IWorkflowEventStore>();
        (await store.LoadTailAsync(
            new WorkflowStreamId(IntegrationIds.Instance(4)),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken))
            .OfType<WorkflowTimerFiredEvent>()
            .Should().ContainSingle(e => e.InstanceId == IntegrationIds.Instance(4));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-012")]
    [Trait("AC", "AC-301")]
    public async Task INT_HO_012_SampleHostDurableWaitSurvivesHostRebuild()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        var management = host.Services.GetRequiredService<DurableManagement>();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.WaitRegistered(1, 10, 2),
            TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        using var restarted = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        var restartedProcessor = restarted.Services.GetRequiredService<DurableCommandProcessor>();
        await restartedProcessor.ProcessAsync(
            IntegrationCommands.Deliver(1, 50, 3),
            TestContext.Current.CancellationToken);
        var snapshot = await restarted.Services.GetRequiredService<DurableManagement>()
            .Instance(IntegrationIds.Instance(1))
            .GetAsync(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowStatus.Running);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-014")]
    public async Task INT_HO_014_CancellationTokenStopsHostedLoops()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var (host, _, _) = await OrcaIntegrationHost.BuildPostgreSqlAsync(
            fixture.ConnectionString,
            cancellationToken: TestContext.Current.CancellationToken);
        using (host)
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
            host.Services.GetServices<IHostedService>().Should().NotBeEmpty();
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-015")]
    [Trait("AC", "OB-010")]
    public async Task INT_HO_015_OutboxPumpObserver_ReceivesHostedPumpSummary()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        var observer = new RecordingOutboxPumpObserver();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher,
            configureServices: services => services.AddSingleton<IOutboxPumpObserver>(observer));
        var pools = host.Services.GetRequiredService<IResourcePoolStore>();
        await pools.UpsertPoolAsync(
            IntegrationCommands.Pool("db", 1),
            TestContext.Current.CancellationToken);
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            IntegrationCommands.RunExternalJob(1, 2, "job-1", IntegrationCommands.Requirement("db")),
            TestContext.Current.CancellationToken);

        var dispatchedCount = await OrcaIntegrationHost.PumpOutboxOnceAsync(
            host,
            TestContext.Current.CancellationToken);

        dispatchedCount.Should().BeGreaterThan(0);
        observer.Observations.Should().ContainSingle()
            .Which.Should().Be(new OutboxPumpObservation(
                ClaimedCount: dispatchedCount,
                DispatchAttemptCount: dispatchedCount,
                SuccessCount: dispatchedCount,
                RetryableFailureCount: 0,
                PermanentFailureCount: 0));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-016")]
    [Trait("AC", "AC-001")]
    public async Task INT_HO_016_HostResolvedDurableProcessor_CompletesWorkflow()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await processor.ProcessAsync(
                IntegrationCommands.StepCompleted(1, 2),
                TestContext.Current.CancellationToken);
            await processor.ProcessAsync(
                IntegrationCommands.Complete(1, 3),
                TestContext.Current.CancellationToken);

            var snapshot = await host.Services.GetRequiredService<DurableManagement>()
                .Instance(IntegrationIds.Instance(1))
                .GetAsync(TestContext.Current.CancellationToken);
            var events = await host.Services.GetRequiredService<IWorkflowEventStore>()
                .LoadTailAsync(
                    new WorkflowStreamId(IntegrationIds.Instance(1)),
                    StreamVersion.Empty,
                    TestContext.Current.CancellationToken);

            snapshot.Status.Should().Be(WorkflowStatus.Completed);
            events.OfType<WorkflowCompletedEvent>().Should().ContainSingle();
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-HO-017")]
    [Trait("AC", "AC-314")]
    public async Task INT_HO_017_HostResolvedManagement_ArchivesTerminalWorkflow()
    {
        await fixture.ResetAsync(TestContext.Current.CancellationToken);
        var dispatcher = new RecordingMessageDispatcher();
        using var host = OrcaIntegrationHost.Build(
            fixture.ConnectionString,
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(IntegrationIds.Timestamp(0)),
            dispatcher);
        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
            var management = host.Services.GetRequiredService<DurableManagement>();
            await processor.ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);
            await management.TerminateAsync(
                IntegrationIds.Instance(1),
                IntegrationIds.Timestamp(2),
                OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
                TestContext.Current.CancellationToken);

            var archived = await management.ArchiveAsync(
                new RetentionPolicy
                {
                    InstanceId = IntegrationIds.Instance(1),
                    RequestedAt = IntegrationIds.Timestamp(3),
                    Reason = "host-level-retention"
                },
                TestContext.Current.CancellationToken);

            archived.Archived.Should().BeTrue();
            var snapshot = await management.Instance(IntegrationIds.Instance(1))
                .GetAsync(TestContext.Current.CancellationToken);
            snapshot.Status.Should().Be(WorkflowStatus.Terminated);
        }
        finally
        {
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private sealed class TestState
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class CompletedStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<TestState> context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RecordingOutboxPumpObserver : IOutboxPumpObserver
    {
        internal List<OutboxPumpObservation> Observations { get; } = [];

        public ValueTask OnPumpCompletedAsync(
            OutboxPumpObservation observation,
            CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            return ValueTask.CompletedTask;
        }
    }
}
