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
            .WhoseValue.Should().Be(activities.Records.Should().ContainSingle().Subject.TraceId);
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

    private static ServiceProvider BuildTelemetryProvider(
        RecordingLoggerProvider logs,
        IMessageDispatcher? dispatcher = null,
        InMemoryWorkflowProvider? workflowProvider = null,
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

    private static async Task WaitForMetricAsync(
        MetricRecorder metrics,
        Func<MetricMeasurement, bool> predicate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            metrics.CollectObservableInstruments();
            if (metrics.Measurements.Any(predicate))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
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
