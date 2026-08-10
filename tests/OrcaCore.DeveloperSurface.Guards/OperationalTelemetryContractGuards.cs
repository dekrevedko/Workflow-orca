using System.Diagnostics.Metrics;
using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OperationalTelemetryContractGuards
{
    private static readonly string[] RequiredGauges =
    [
        OrcaCoreMetrics.InstancesActive,
        OrcaCoreMetrics.InstancesStuck,
        OrcaCoreMetrics.WaitsActive,
        OrcaCoreMetrics.OutboxPending,
        OrcaCoreMetrics.StreamEvents,
        OrcaCoreMetrics.CheckpointsCount,
        OrcaCoreMetrics.CheckpointsLag,
        OrcaCoreMetrics.ResourcePoolWaiters,
        OrcaCoreMetrics.ResourcePoolTickets,
        OrcaCoreMetrics.ResourcePoolReservedUnits,
        OrcaCoreMetrics.ResourcePoolOverCapacityDebt,
        OrcaCoreMetrics.ResourcePoolReconciliationDue,
        OrcaCoreMetrics.ContinuationPendingCount,
        OrcaCoreMetrics.ExternalOutboxPendingCount
    ];

    private static readonly string[] RequiredCounters =
    [
        OrcaCoreMetrics.CommandsProcessed,
        OrcaCoreMetrics.EventsApplied,
        OrcaCoreMetrics.StepsCompleted,
        OrcaCoreMetrics.StepsFailed,
        OrcaCoreMetrics.OutboxDispatched,
        OrcaCoreMetrics.ResourcePoolReconciliations,
        OrcaCoreMetrics.LifecycleEvents,
        OrcaCoreMetrics.InboxDuplicates,
        OrcaCoreMetrics.DriverPoisonCount,
        OrcaCoreMetrics.DriverRegistrationConflictCount
    ];

    private static readonly string[] RequiredHistograms =
    [
        OrcaCoreMetrics.CommandsDuration,
        OrcaCoreMetrics.StepsDuration,
        OrcaCoreMetrics.ProviderCommitDuration,
        OrcaCoreMetrics.OutboxDispatchDuration,
        OrcaCoreMetrics.WaitsDuration,
        OrcaCoreMetrics.DriverSegmentDuration,
        OrcaCoreMetrics.ContinuationLag
    ];

    [Fact]
    public void RuntimeOwners_PublishTheCompleteCanonicalOrcaMetricCatalog()
    {
        var published = new Dictionary<string, Instrument>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name is OrcaCoreDiagnostics.DurableSourceName or
                    OrcaCoreDiagnostics.EphemeralSourceName)
                {
                    published[instrument.Name] = instrument;
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.Start();
        LoadDiagnosticsMeter("OrcaCore.Engine.Durable", "OrcaCore.Engine.Durable.Diagnostics.OrcaCoreDurableDiagnostics");
        LoadDiagnosticsMeter(
            "OrcaCore.Engine.Ephemeral",
            "OrcaCore.Engine.Ephemeral.Diagnostics.OrcaCoreEphemeralDiagnostics");

        published.Keys.Should().Contain(RequiredGauges);
        published.Keys.Should().Contain(RequiredCounters);
        published.Keys.Should().Contain(RequiredHistograms);
        published.Keys.Should().OnlyContain(name => name.StartsWith("orca.", StringComparison.Ordinal));
        RequiredGauges.Should().OnlyContain(name => published[name] is ObservableGauge<long>);
        RequiredCounters.Should().OnlyContain(name => published[name] is Counter<long>);
        RequiredHistograms.Should().OnlyContain(name => published[name] is Histogram<double>);
    }

    [Fact]
    public void DurableGauges_UseTheSameAuthoritativeStatisticsSnapshotWithExactTags()
    {
        var observed = new List<ObservedMeasurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName)
                {
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            observed.Add(new ObservedMeasurement(instrument.Name, value, tags.ToArray())));
        listener.Start();
        var diagnostics = Assembly.Load("OrcaCore.Engine.Durable")
            .GetType("OrcaCore.Engine.Durable.Diagnostics.OrcaCoreDurableDiagnostics", throwOnError: true)!;
        _ = diagnostics.GetProperty("Meter", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        var definitionId = global::OrcaCore.DefinitionId.Parse("00000000-0000-0000-0000-000000000171");
        var instanceId = global::OrcaCore.InstanceId.Parse("00000000-0000-0000-0000-000000000172");
        var waitEventName = global::OrcaCore.EventName.Create("operator-wait");
        var statistics = new WorkflowOperatorStatistics
        {
            ProviderName = "certified",
            Groups =
            [
                new WorkflowOperatorStatisticsGroup(
                    definitionId,
                    global::OrcaCore.DefinitionVersion.Initial,
                    global::OrcaCore.WorkflowInstanceStatus.Waiting,
                    3)
            ],
            StuckGroups = [new WorkflowOperatorStuckGroup(definitionId, 1)],
            ActiveWaitGroups = [new WorkflowOperatorActiveWaitGroup(definitionId, waitEventName, 2)],
            Pressure = new WorkflowOperationalPressure
            {
                ActiveInstanceCount = 3,
                StuckInstanceCount = 1,
                ActiveWaitCount = 2,
                StreamEventCount = 11,
                CheckpointCount = 4,
                CheckpointLag = 7,
                ContinuationPendingCount = 5,
                ContinuationPoisonedCount = 1,
                ExternalOutboxRetryableCount = 6,
                ExternalOutboxClaimedCount = 2
            }
        };
        var pool = new ResourcePoolSnapshot(
            "operator-pool",
            10,
            5,
            [
                new ResourcePoolTicket(
                    Guid.Parse("00000000-0000-0000-0000-000000000173"),
                    "operator-pool",
                    3,
                    instanceId,
                    "held-ticket",
                    DateTimeOffset.UnixEpoch,
                    null),
                new ResourcePoolTicket(
                    Guid.Parse("00000000-0000-0000-0000-000000000174"),
                    "operator-pool",
                    2,
                    instanceId,
                    "review-ticket",
                    DateTimeOffset.UnixEpoch,
                    null)
                {
                    ReviewMarked = true
                }
            ],
            []);
        diagnostics.GetMethod("RefreshOperatorStatistics", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [statistics, new[] { pool }]);
        listener.RecordObservableInstruments();

        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesActive &&
            measurement.Value == 3 &&
            measurement.HasTag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode) &&
            measurement.HasTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()) &&
            measurement.HasTag(OrcaCoreDiagnostics.StatusKey, global::OrcaCore.WorkflowInstanceStatus.Waiting.ToString()));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.InstancesStuck &&
            measurement.Value == statistics.Pressure.StuckInstanceCount &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode),
                Tag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString())));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.WaitsActive &&
            measurement.Value == statistics.Pressure.ActiveWaitCount &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode),
                Tag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()),
                Tag(OrcaCoreDiagnostics.WaitEventNameKey, waitEventName.Value)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StreamEvents &&
            measurement.Value == statistics.Pressure.StreamEventCount &&
            measurement.HasTag(OrcaCoreDiagnostics.ProviderNameKey, statistics.ProviderName));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ContinuationPendingCount &&
            measurement.Value == statistics.Pressure.ContinuationPoisonedCount &&
            measurement.HasTag(OrcaCoreDiagnostics.QueueLaneKey, OrcaCoreDiagnostics.ContinuationQueueLane) &&
            measurement.HasTag(
                OrcaCoreDiagnostics.OutboxStateKey,
                OrcaCoreDiagnostics.PoisonedOutboxState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ExternalOutboxPendingCount &&
            measurement.Value == statistics.Pressure.ExternalOutboxRetryableCount &&
            measurement.HasTag(OrcaCoreDiagnostics.QueueLaneKey, OrcaCoreDiagnostics.ExternalOutboxQueueLane) &&
            measurement.HasTag(
                OrcaCoreDiagnostics.OutboxStateKey,
                OrcaCoreDiagnostics.RetryableOutboxState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolTickets &&
            measurement.Value == 1 &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name),
                Tag(OrcaCoreDiagnostics.ResourcePoolStateKey, OrcaCoreDiagnostics.HeldResourceState)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolReservedUnits &&
            measurement.Value == 3 &&
            measurement.HasExactTags(
                Tag(OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name),
                Tag(OrcaCoreDiagnostics.ResourcePoolStateKey, OrcaCoreDiagnostics.HeldResourceState)));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolTickets &&
            measurement.Value == 1 &&
            measurement.HasTag(
                OrcaCoreDiagnostics.ResourcePoolStateKey,
                OrcaCoreDiagnostics.ReviewMarkedResourceState));
        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.ResourcePoolReservedUnits &&
            measurement.Value == 2 &&
            measurement.HasTag(
                OrcaCoreDiagnostics.ResourcePoolStateKey,
                OrcaCoreDiagnostics.ReviewMarkedResourceState));
    }

    [Fact]
    public void Product_EmitsTheExactBclSpanAndStructuredLogFamiliesWithoutAnSdkDependency()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var durableSource = ReadSource(Path.Combine(root, "src", "OrcaCore.Engine.Durable"));
        var hostingSource = ReadSource(Path.Combine(root, "src", "OrcaCore.Durable.Hosting"));
        foreach (var spanOwner in new[]
        {
            nameof(OrcaCoreDiagnostics.CommandProcessActivity),
            nameof(OrcaCoreDiagnostics.ProviderCommitActivity),
            nameof(OrcaCoreDiagnostics.StepExecuteActivity),
            nameof(OrcaCoreDiagnostics.OutboxDispatchActivity),
            nameof(OrcaCoreDiagnostics.OutboxPumpCycleActivity),
            nameof(OrcaCoreDiagnostics.EventApplyActivity)
        })
        {
            durableSource.Should().Contain($"OrcaCoreDiagnostics.{spanOwner}");
        }

        hostingSource.Should().Contain("[LoggerMessage(");
        hostingSource.Should().Contain(nameof(OrcaCoreDiagnostics.CommandTypeKey));
        hostingSource.Should().Contain(nameof(OrcaCoreDiagnostics.OutboxRecordIdKey));
        Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Should().OnlyContain(text => !text.Contains("OpenTelemetry", StringComparison.Ordinal));
    }

    private static void LoadDiagnosticsMeter(string assemblyName, string typeName) =>
        _ = Assembly.Load(assemblyName)
            .GetType(typeName, throwOnError: true)!
            .GetProperty("Meter", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

    private static string ReadSource(string path) =>
        string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

    private static KeyValuePair<string, object?> Tag(string key, object value) => new(key, value);

    private sealed record ObservedMeasurement(
        string Name,
        long Value,
        KeyValuePair<string, object?>[] Tags)
    {
        internal bool HasTag(string key, object value) =>
            Tags.Any(tag => string.Equals(tag.Key, key, StringComparison.Ordinal) && Equals(tag.Value, value));

        internal bool HasExactTags(params KeyValuePair<string, object?>[] expected) =>
            Tags.Length == expected.Length &&
            expected.All(item => HasTag(item.Key, item.Value!));
    }
}
