using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Hosting;
using OrcaCore.Integration.Tests.Support;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport;
using OrcaCore.TestSupport.Providers;

namespace OrcaCore.Integration.Tests.Observability;

[Trait(Traits.Category, Traits.Integration)]
public sealed class ObservabilityIntegrationTests
{
    [Fact]
    [Trait(Traits.Scenario, "INT-OB-001")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-001")]
    public async Task INT_OB_001_MetricsEmitted_ForDurableCommands()
    {
        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs);

        await provider.GetRequiredService<DurableCommandProcessor>()
            .ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.CommandsProcessedName &&
            HasTag(measurement.Tags, "orca.command.type", nameof(StartWorkflowCommand)) &&
            HasTag(measurement.Tags, "outcome", DurableCommandOutcome.Committed.ToString()));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-002")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-002")]
    public async Task INT_OB_002_StructuredCommandLog_IncludesCommandInstanceAndTrace()
    {
        using var activities = new ActivityRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs);

        await provider.GetRequiredService<DurableCommandProcessor>()
            .ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var commandLog = logs.Records.Should().Contain(record =>
            record.EventId == 1001 &&
            HasProperty(record.Properties, "orca.command.type", nameof(StartWorkflowCommand))).Subject;
        commandLog.Properties.Should().ContainKey("orca.instance.id");
        commandLog.Properties.Should().ContainKey("trace_id")
            .WhoseValue.Should().Be(activities.Records.Should().Contain(record =>
                record.Name == "orca.command.process").Subject.TraceId);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-003")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-003")]
    public async Task INT_OB_003_FailedOutboxDispatch_EmitsMetricAndStructuredErrorLog()
    {
        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(
            logs,
            new FakeMessageDispatcher(DispatchResult.PermanentFailure));
        var store = provider.GetRequiredService<IWorkflowEventStore>();
        await store.AppendAsync(
            IntegrationCommands.OutboxOnlyBatch(
                31,
                new OutboxWrite(IntegrationIds.Outbox(31), "workflow.completed", [1])),
            TestContext.Current.CancellationToken);

        await provider.GetRequiredService<DurableOutboxPump>()
            .PumpOnceAsync(10, TestContext.Current.CancellationToken);

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.OutboxDispatchedName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.outbox.kind", "workflow.completed") &&
            HasTag(measurement.Tags, "result", "permanent"));
        logs.Records.Should().Contain(record =>
            record.EventId == 1103 &&
            record.Level == LogLevel.Error &&
            HasProperty(record.Properties, "orca.outbox.kind", "workflow.completed"));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-004")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-004")]
    public async Task INT_OB_004_CommandActivity_CarriesInstanceAttributesForTraceLink()
    {
        using var activities = new ActivityRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs);

        await provider.GetRequiredService<DurableCommandProcessor>()
            .ProcessAsync(IntegrationCommands.Start(), TestContext.Current.CancellationToken);

        var activity = activities.Records.Should().ContainSingle(record =>
            record.Name == "orca.command.process").Subject;
        activity.Tags.Should().ContainKey("orca.command.type")
            .WhoseValue.Should().Be(nameof(StartWorkflowCommand));
        activity.Tags.Should().ContainKey("orca.instance.id");
        activity.Tags.Should().ContainKey("outcome")
            .WhoseValue.Should().Be(DurableCommandOutcome.Committed.ToString());
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-005")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-005")]
    public async Task INT_OB_005_StatisticsParity_ActiveInstanceGaugeMatchesManagementStats()
    {
        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs, enableOpenTelemetry: true);
        var definitionId = DefinitionId.New();
        var definitionVersion = DefinitionVersion.Initial;
        await provider.GetRequiredService<DurableCommandProcessor>()
            .ProcessAsync(
                new StartWorkflowCommand
                {
                    CommandId = CommandId.New(),
                    InstanceId = InstanceId.New(),
                    RequestedAt = IntegrationIds.Timestamp(1),
                    DefinitionId = definitionId,
                    DefinitionVersion = definitionVersion
                },
                TestContext.Current.CancellationToken);

        var stats = await provider.GetRequiredService<DurableManagement>()
            .All()
            .StatisticsAsync(TestContext.Current.CancellationToken);
        var expectedCount = stats.Groups.Single(group =>
            group.DefinitionId == definitionId &&
            group.DefinitionVersion == definitionVersion &&
            group.Status == WorkflowStatus.Running).Count;
        var gaugeCollector = GetGaugeCollector(provider);

        await gaugeCollector.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForMetricAsync(
                metrics,
                measurement =>
                    measurement.Name == OrcaCoreMetrics.InstancesActiveName &&
                    HasTag(measurement.Tags, "orca.definition.id", definitionId.ToString()) &&
                    HasTag(measurement.Tags, "orca.definition.version", definitionVersion.ToString()) &&
                    HasTag(measurement.Tags, "orca.status", WorkflowStatus.Running.ToString()),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            await gaugeCollector.StopAsync(CancellationToken.None);
        }

        var metricCount = metrics.Measurements
            .Where(measurement =>
                measurement.Name == OrcaCoreMetrics.InstancesActiveName &&
                measurement.Tags.TryGetValue("orca.definition.id", out var metricDefinitionId) &&
                metricDefinitionId == definitionId.ToString() &&
                measurement.Tags.TryGetValue("orca.definition.version", out var metricDefinitionVersion) &&
                metricDefinitionVersion == definitionVersion.ToString() &&
                measurement.Tags.TryGetValue("orca.status", out var status) &&
                status == WorkflowStatus.Running.ToString())
            .Sum(measurement => measurement.Value);

        metricCount.Should().Be(expectedCount);
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-006")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-006")]
    public async Task INT_OB_006_DefaultPumpObserver_RecordsDispatchMetrics()
    {
        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs, new FakeMessageDispatcher());
        await provider.GetRequiredService<IWorkflowEventStore>()
            .AppendAsync(
                IntegrationCommands.OutboxOnlyBatch(
                    61,
                    new OutboxWrite(IntegrationIds.Outbox(61), "workflow.completed", [6])),
                TestContext.Current.CancellationToken);

        await provider.GetRequiredService<DurableOutboxPump>()
            .PumpOnceAsync(10, TestContext.Current.CancellationToken);

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.OutboxDispatchedName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.outbox.kind", "workflow.completed") &&
            HasTag(measurement.Tags, "result", "success"));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-007")]
    [Trait(Traits.AcceptanceCriteria, "OB-AC-007")]
    public void INT_OB_007_NoOpenTelemetryPackagesOutsideHosting()
    {
        var root = FindV3Root();
        var nonHostingProjects = Directory
            .GetFiles(Path.Combine(root.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(
                Path.Combine("OrcaCore.Hosting", "OrcaCore.Hosting.csproj"),
                StringComparison.OrdinalIgnoreCase));

        foreach (var project in nonHostingProjects)
        {
            File.ReadAllText(project).Should().NotContain("OpenTelemetry.");
        }
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-008")]
    [Trait(Traits.AcceptanceCriteria, "OB-070")]
    public void INT_OB_008_HostingExtensionRegistersOpenTelemetryBridge()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OrcaCore:OpenTelemetry:EnableConsoleExporter"] = "false",
                ["OrcaCore:OpenTelemetry:TraceSamplingRatio"] = "1",
                ["OrcaCore:OpenTelemetry:ServiceName"] = "orca-test-host"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddOrcaCore();
        services.AddOrcaCoreOpenTelemetry(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkflowRuntimeObserver>().Should().NotBeNull();
        provider.GetServices<IHostedService>()
            .Should()
            .Contain(service => service.GetType().Name == "OrcaCoreTelemetryGaugeCollector");
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-009")]
    [Trait(Traits.AcceptanceCriteria, "OB-080")]
    public async Task INT_OB_009_ActiveGauge_IsProjectionBackedWithoutCommandTally()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var instanceId = InstanceId.New();
        await store.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = new WorkflowInstanceSnapshot
                    {
                        InstanceId = instanceId,
                        DefinitionId = definitionId,
                        DefinitionVersion = DefinitionVersion.Initial,
                        Status = WorkflowStatus.Waiting,
                        CreatedAt = IntegrationIds.Timestamp(1),
                        UpdatedAt = IntegrationIds.Timestamp(2)
                    }
                }
            ],
            TestContext.Current.CancellationToken);
        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs, workflowProvider: store, enableOpenTelemetry: true);
        provider.GetRequiredService<IWorkflowRuntimeObserver>().Should().NotBeNull();
        var gaugeCollector = GetGaugeCollector(provider);

        await gaugeCollector.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            await WaitForMetricAsync(
                metrics,
                measurement =>
                    measurement.Name == OrcaCoreMetrics.InstancesActiveName &&
                    measurement.Value == 1 &&
                    HasTag(measurement.Tags, "orca.definition.id", definitionId.ToString()) &&
                    HasTag(measurement.Tags, "orca.status", WorkflowStatus.Waiting.ToString()),
                TestContext.Current.CancellationToken);
        }
        finally
        {
            await gaugeCollector.StopAsync(CancellationToken.None);
        }

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesActiveName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.definition.id", definitionId.ToString()) &&
            HasTag(measurement.Tags, "orca.status", WorkflowStatus.Waiting.ToString()));
        metrics.Measurements.Should().NotContain(measurement =>
            measurement.Name == OrcaCoreMetrics.CommandsProcessedName &&
            HasTag(measurement.Tags, "orca.definition.id", definitionId.ToString()));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-010")]
    [Trait(Traits.AcceptanceCriteria, "OB-050")]
    public async Task INT_OB_010_OutboxSpansUseCatalogNamesAndAttributes()
    {
        using var activities = new ActivityRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs, new FakeMessageDispatcher());
        await provider.GetRequiredService<IWorkflowEventStore>()
            .AppendAsync(
                IntegrationCommands.OutboxOnlyBatch(
                    101,
                    new OutboxWrite(IntegrationIds.Outbox(101), "workflow.completed", [10])),
                TestContext.Current.CancellationToken);

        await provider.GetRequiredService<DurableOutboxPump>()
            .PumpOnceAsync(10, TestContext.Current.CancellationToken);

        activities.Records.Should().Contain(record =>
            record.Name == "orca.outbox.pump_cycle" &&
            record.Tags.ContainsKey("orca.outbox.max_count") &&
            record.Tags.ContainsKey("orca.outbox.claimed_count"));
        activities.Records.Should().Contain(record =>
            record.Name == "orca.outbox.dispatch" &&
            HasTag(record.Tags, "orca.outbox.kind", "workflow.completed") &&
            HasTag(record.Tags, "result", "success"));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-011")]
    [Trait(Traits.AcceptanceCriteria, "OB-072")]
    public void INT_OB_011_DashboardIsReferenceSampleAndGrafanaArtifactExists()
    {
        var root = FindV3Root();

        File.Exists(Path.Combine(root.FullName, "dashboard", "OrcaCore.Dashboard.csproj"))
            .Should().BeFalse("OrcaCore should not ship a built-in dashboard app");
        var sampleReadme = Path.Combine(root.FullName, "samples", "OrcaCore.Dashboard", "README.md");
        File.Exists(sampleReadme).Should().BeTrue();
        File.ReadAllText(sampleReadme).Should().Contain("not a built-in OrcaCore product dashboard");
        File.Exists(Path.Combine(
                root.FullName,
                "docs",
                "observability",
                "dashboards",
                "orca-core-overview.json"))
            .Should().BeTrue();
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-012")]
    [Trait(Traits.AcceptanceCriteria, "OB-021;OB-022;OB-023;OB-050")]
    public async Task INT_OB_012_CatalogSignalsUseDurableRuntimeObservations()
    {
        using var metrics = new MetricRecorder();
        using var activities = new ActivityRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(logs);
        var processor = provider.GetRequiredService<DurableCommandProcessor>();
        var token = TestContext.Current.CancellationToken;

        await processor.ProcessAsync(IntegrationCommands.Start(instance: 1201, command: 1201), token);
        await processor.ProcessAsync(
            IntegrationCommands.StepCompleted(instance: 1201, command: 1202, stepPath: "root/approve"),
            token);

        await processor.ProcessAsync(IntegrationCommands.Start(instance: 1202, command: 1203), token);
        await processor.ProcessAsync(
            new DurableStepFailedCommand(
                IntegrationIds.Command(1204),
                IntegrationIds.Instance(1202),
                IntegrationIds.Timestamp(1204),
                "root/fail",
                "boom"),
            token);

        await processor.ProcessAsync(IntegrationCommands.Start(instance: 1203, command: 1205), token);
        await processor.ProcessAsync(IntegrationCommands.WaitRegistered(1203, 1206, 1206), token);
        await processor.ProcessAsync(IntegrationCommands.Deliver(1203, 1207, 1207), token);
        await processor.ProcessAsync(IntegrationCommands.Deliver(1203, 1207, 1208), token);

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.EventsAppliedName &&
            HasTag(measurement.Tags, "orca.event.type", nameof(WorkflowStepCompletedEvent)));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StepsCompletedName &&
            HasTag(measurement.Tags, "orca.step.path", "root/approve"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StepsFailedName &&
            HasTag(measurement.Tags, "orca.step.path", "root/fail") &&
            HasTag(measurement.Tags, "error.kind", nameof(WorkflowStepFailedEvent)));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.LifecycleEventsName &&
            HasTag(measurement.Tags, "event.name", "StepCompleted") &&
            HasTag(measurement.Tags, "durable", bool.TrueString));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InboxDuplicatesName &&
            HasTag(measurement.Tags, "orca.execution.mode", "durable"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ProviderCommitDurationName &&
            HasTag(measurement.Tags, "orca.provider.name", "InMemory") &&
            HasTag(measurement.Tags, "operation", "append"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StepsDurationName &&
            HasTag(measurement.Tags, "orca.step.path", "root/approve"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.WaitsDurationName &&
            HasTag(measurement.Tags, "orca.wait.event_name", "Approved"));

        activities.Records.Should().Contain(record =>
            record.Name == "orca.provider.commit" &&
            HasTag(record.Tags, "orca.provider.name", "InMemory") &&
            HasTag(record.Tags, "operation", "append"));
        activities.Records.Should().Contain(record =>
            record.Name == "orca.event.apply" &&
            HasTag(record.Tags, "orca.event.type", nameof(WorkflowStepCompletedEvent)));
        activities.Records.Should().Contain(record =>
            record.Name == "orca.step.execute" &&
            HasTag(record.Tags, "orca.step.path", "root/approve"));
    }

    [Fact]
    [Trait(Traits.Scenario, "INT-OB-013")]
    [Trait(Traits.AcceptanceCriteria, "OB-020;OB-021;OB-080")]
    public async Task INT_OB_013_CatalogGaugesUseProviderAndResourcePoolState()
    {
        var workflowProvider = new InMemoryWorkflowProvider();
        var resourcePools = new InMemoryResourcePoolStore();
        var definitionId = IntegrationIds.Definition(1301);
        var instanceId = IntegrationIds.Instance(1301);
        var token = TestContext.Current.CancellationToken;
        await SeedProviderPressureAsync(workflowProvider, definitionId, instanceId, token);
        await SeedResourcePoolPressureAsync(resourcePools, token);

        using var metrics = new MetricRecorder();
        using var logs = new RecordingLoggerProvider();
        using var provider = BuildTelemetryProvider(
            logs,
            workflowProvider: workflowProvider,
            resourcePoolStore: resourcePools,
            enableOpenTelemetry: true);
        var gaugeCollector = GetGaugeCollector(provider);

        await gaugeCollector.StartAsync(token);
        try
        {
            await WaitForMetricAsync(
                metrics,
                measurement =>
                    measurement.Name == OrcaCoreMetrics.ResourcePoolWaitersName &&
                    measurement.Value == 1 &&
                    HasTag(measurement.Tags, "pool.name", "db"),
                token);
        }
        finally
        {
            await gaugeCollector.StopAsync(CancellationToken.None);
        }

        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesStuckName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.definition.id", definitionId.ToString()));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.WaitsActiveName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.wait.event_name", "approval.received"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.OutboxPendingName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "state", "pending"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.OutboxPendingName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "state", "retryable"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.OutboxPendingName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "state", "claimed"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StreamEventsName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.provider.name", "InMemory"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.CheckpointsCountName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.provider.name", "InMemory"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.CheckpointsLagName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "orca.provider.name", "InMemory"));
        metrics.Measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolTicketsName &&
            measurement.Value == 1 &&
            HasTag(measurement.Tags, "pool.name", "db"));
        OrcaCoreDiagnostics.MeterNames.Should().Contain(OrcaCoreDiagnostics.InMemoryProviderSourceName);
        OrcaCoreDiagnostics.ActivitySourceNames.Should().Contain(OrcaCoreDiagnostics.SqlServerProviderSourceName);
    }

    private static ServiceProvider BuildTelemetryProvider(
        RecordingLoggerProvider logs,
        IMessageDispatcher? dispatcher = null,
        InMemoryWorkflowProvider? workflowProvider = null,
        InMemoryResourcePoolStore? resourcePoolStore = null,
        bool enableOpenTelemetry = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(logs);
        });
        if (workflowProvider is not null)
        {
            services.AddSingleton(workflowProvider);
        }

        if (resourcePoolStore is not null)
        {
            services.AddSingleton(resourcePoolStore);
        }

        services.AddOrcaCore();
        if (enableOpenTelemetry)
        {
            services.AddOrcaCoreOpenTelemetry(options =>
                options.GaugeCollectionInterval = TimeSpan.FromMilliseconds(50));
        }

        if (dispatcher is not null)
        {
            services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher>(dispatcher));
        }

        return services.BuildServiceProvider();
    }

    private static async Task SeedProviderPressureAsync(
        InMemoryWorkflowProvider workflowProvider,
        DefinitionId definitionId,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var activeWait = new ActiveWaitSnapshot
        {
            WaitId = IntegrationIds.Wait(1301),
            EventName = "approval.received",
            CorrelationId = new CorrelationId("order-1301"),
            RegisteredAt = IntegrationIds.Timestamp(1301),
            Status = "Active",
            Mode = WaitMode.Resident.ToString()
        };

        await workflowProvider.ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                {
                    InstanceSnapshot = new WorkflowInstanceSnapshot
                    {
                        InstanceId = instanceId,
                        DefinitionId = definitionId,
                        DefinitionVersion = DefinitionVersion.Initial,
                        Status = WorkflowStatus.Waiting,
                        CreatedAt = IntegrationIds.Timestamp(1301),
                        UpdatedAt = IntegrationIds.Timestamp(1302),
                        IsStuck = true,
                        ActiveWaits = [activeWait]
                    }
                }
            ],
            cancellationToken);

        var pending = IntegrationIds.Outbox(1301);
        var retryable = IntegrationIds.Outbox(1302);
        var claimed = IntegrationIds.Outbox(1303);
        var append = await workflowProvider.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events =
                [
                    new WorkflowStartedEvent
                    {
                        EventId = IntegrationIds.Event(1301),
                        InstanceId = instanceId,
                        CommandId = IntegrationIds.Command(1301),
                        CausationId = IntegrationIds.Causation(1301),
                        OccurredAt = IntegrationIds.Timestamp(1301),
                        DefinitionId = definitionId,
                        DefinitionVersion = DefinitionVersion.Initial
                    }
                ],
                Checkpoint = new CheckpointWrite(
                    instanceId,
                    StreamVersion.Empty,
                    "application/json",
                    []),
                OutboxRecords =
                [
                    new OutboxWrite(pending, "pending-kind", []),
                    new OutboxWrite(retryable, "retryable-kind", []),
                    new OutboxWrite(claimed, "claimed-kind", [])
                ]
            },
            cancellationToken);
        append.IsSuccess.Should().BeTrue();

        await workflowProvider.MarkAsync(retryable, OutboxRecordState.Retryable, cancellationToken);
        await workflowProvider.MarkAsync(claimed, OutboxRecordState.Claimed, cancellationToken);
    }

    private static async Task SeedResourcePoolPressureAsync(
        InMemoryResourcePoolStore resourcePools,
        CancellationToken cancellationToken)
    {
        await resourcePools.UpsertPoolAsync(IntegrationCommands.Pool("db", 1), cancellationToken);
        await resourcePools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                IntegrationIds.Instance(1311),
                "node-1",
                [IntegrationCommands.Requirement("db")],
                IntegrationIds.Timestamp(1311),
                IntegrationIds.Timestamp(1341)),
            cancellationToken);
        await resourcePools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                IntegrationIds.Instance(1312),
                "node-2",
                [IntegrationCommands.Requirement("db")],
                IntegrationIds.Timestamp(1312),
                IntegrationIds.Timestamp(1342)),
            cancellationToken);
    }

    private static async Task WaitForMetricAsync(
        MetricRecorder metrics,
        Func<MetricMeasurement, bool> predicate,
        CancellationToken cancellationToken)
    {
        // The gauge collector performs its first collection immediately on StartAsync (before its
        // periodic timer), reading in-memory projections. Yield until that async collection surfaces
        // the metric rather than sleeping on the wall clock; a genuine miss is bounded by the test's
        // own cancellation timeout.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            metrics.CollectObservableInstruments();
            if (metrics.Measurements.Any(predicate))
            {
                return;
            }

            await Task.Yield();
        }
    }

    private static IHostedService GetGaugeCollector(IServiceProvider provider)
    {
        return provider.GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "OrcaCoreTelemetryGaugeCollector");
    }

    private static DirectoryInfo FindV3Root()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OrcaCore.slnx")) &&
                File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the v3-gpt workspace root.");
    }

    private sealed class MetricRecorder : IDisposable
    {
        private readonly MeterListener listener = new();

        internal MetricRecorder()
        {
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (OrcaCoreDiagnostics.MeterNames.Contains(instrument.Meter.Name))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>(Record);
            listener.SetMeasurementEventCallback<double>(Record);
            listener.Start();
        }

        internal ConcurrentQueue<MetricMeasurement> Measurements { get; } = new();

        internal void CollectObservableInstruments()
        {
            listener.RecordObservableInstruments();
        }

        public void Dispose()
        {
            listener.Dispose();
        }

        private void Record<T>(
            Instrument instrument,
            T measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags,
            object? state)
            where T : struct
        {
            Measurements.Enqueue(new MetricMeasurement(
                instrument.Name,
                Convert.ToDouble(measurement),
                CaptureTags(tags)));
        }
    }

    private sealed class ActivityRecorder : IDisposable
    {
        private readonly ActivityListener listener;

        internal ActivityRecorder()
        {
            listener = new ActivityListener
            {
                ShouldListenTo = source => OrcaCoreDiagnostics.ActivitySourceNames.Contains(source.Name),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Records.Enqueue(new ActivityRecord(
                    activity.OperationName,
                    activity.TraceId.ToString(),
                    CaptureTags(activity.TagObjects)))
            };
            ActivitySource.AddActivityListener(listener);
        }

        internal ConcurrentQueue<ActivityRecord> Records { get; } = new();

        public void Dispose()
        {
            listener.Dispose();
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly AsyncLocal<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> scopes = new();

        internal ConcurrentQueue<LogRecord> Records { get; } = new();

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(this, categoryName);
        }

        public void Dispose()
        {
        }

        private IDisposable PushScope(IReadOnlyDictionary<string, object?> scope)
        {
            var prior = scopes.Value;
            scopes.Value = prior is null
                ? [scope]
                : [.. prior, scope];
            return new ScopePopper(() => scopes.Value = prior);
        }

        private void Record<TState>(
            string categoryName,
            LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (scopes.Value is not null)
            {
                foreach (var scope in scopes.Value)
                {
                    foreach (var (key, value) in scope)
                    {
                        properties[key] = value;
                    }
                }
            }

            foreach (var (key, value) in CaptureState(state))
            {
                properties[key] = value;
            }

            Records.Enqueue(new LogRecord(
                categoryName,
                logLevel,
                eventId.Id,
                formatter(state, exception),
                properties));
        }

        private sealed class RecordingLogger(
            RecordingLoggerProvider provider,
            string categoryName) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return provider.PushScope(CaptureState(state));
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                Microsoft.Extensions.Logging.EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Record(categoryName, logLevel, eventId, state, exception, formatter);
            }
        }

        private sealed class ScopePopper(Action pop) : IDisposable
        {
            public void Dispose()
            {
                pop();
            }
        }
    }

    private sealed record MetricMeasurement(
        string Name,
        double Value,
        IReadOnlyDictionary<string, string> Tags);

    private sealed record ActivityRecord(
        string Name,
        string TraceId,
        IReadOnlyDictionary<string, string> Tags);

    private sealed record LogRecord(
        string CategoryName,
        LogLevel Level,
        int EventId,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private static IReadOnlyDictionary<string, string> CaptureTags(
        IEnumerable<KeyValuePair<string, object?>> tags)
    {
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in tags)
        {
            captured[key] = value?.ToString() ?? string.Empty;
        }

        return captured;
    }

    private static IReadOnlyDictionary<string, string> CaptureTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            captured[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        return captured;
    }

    private static IReadOnlyDictionary<string, object?> CaptureState<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return pairs.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static bool HasTag(
        IReadOnlyDictionary<string, string> tags,
        string key,
        string expected)
    {
        return tags.TryGetValue(key, out var actual) &&
            actual == expected;
    }

    private static bool HasProperty(
        IReadOnlyDictionary<string, object?> properties,
        string key,
        object? expected)
    {
        return properties.TryGetValue(key, out var actual) &&
            Equals(actual, expected);
    }
}
