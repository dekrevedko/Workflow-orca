using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Engine.Ephemeral.Diagnostics;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests;

public sealed class EphemeralOperatorStatisticsTelemetryTests
{
    private static readonly string[] RequiredInstrumentNames =
    [
        OrcaCoreMetrics.CommandsProcessed,
        OrcaCoreMetrics.EventsApplied,
        OrcaCoreMetrics.StepsCompleted,
        OrcaCoreMetrics.StepsFailed,
        OrcaCoreMetrics.OutboxDispatched,
        OrcaCoreMetrics.ResourcePoolReconciliations,
        OrcaCoreMetrics.LifecycleEvents,
        OrcaCoreMetrics.InboxDuplicates,
        OrcaCoreMetrics.CommandsDuration,
        OrcaCoreMetrics.StepsDuration,
        OrcaCoreMetrics.ProviderCommitDuration,
        OrcaCoreMetrics.OutboxDispatchDuration,
        OrcaCoreMetrics.WaitsDuration,
        OrcaCoreMetrics.InstancesActive,
        OrcaCoreMetrics.InstancesStuck,
        OrcaCoreMetrics.WaitsActive,
        OrcaCoreMetrics.GovernanceConfiguredLimit,
        OrcaCoreMetrics.GovernanceActiveSlots,
        OrcaCoreMetrics.GovernanceWaitDepth,
        OrcaCoreMetrics.GovernanceCancellations
    ];

    [Fact]
    public void Meter_PublishesExactlyTheCanonicalEphemeralInstrumentCatalog()
    {
        var published = new ConcurrentDictionary<string, Instrument>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.EphemeralSourceName)
                {
                    published[instrument.Name] = instrument;
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.Start();
        _ = new EphemeralWorkflowEngine(TimeProvider.System);

        published.Keys.OrderBy(name => name, StringComparer.Ordinal)
            .Should().Equal(RequiredInstrumentNames.OrderBy(name => name, StringComparer.Ordinal));
        published.Keys.Should().OnlyContain(name => name.StartsWith("orca.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_UpdatesGroupedStatisticsAndCanonicalBclDiagnostics()
    {
        var activities = new ConcurrentQueue<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == OrcaCoreEphemeralDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(activityListener);
        var measurements = new ConcurrentQueue<(string Name, long Value)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OrcaCoreEphemeralDiagnostics.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            measurements.Enqueue((instrument.Name, value)));
        meterListener.Start();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(
                global::OrcaCore.DefinitionId.New(),
                global::OrcaCore.DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .End()
            .Build();
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        }).AddWorkflow(definition);
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<global::OrcaCore.IWorkflowDefinitionRegistry>();
        var handle = registry.GetRequiredHandle(definition.Reference);

        _ = await handle.StartOrGetAsync(
            "start",
            global::OrcaCore.StartIdempotencyKey.Create("operator-statistics-telemetry"),
            TestContext.Current.CancellationToken);

        activities.Any(activity =>
            activity.OperationName == "orca.command.process" &&
            string.Equals(
                activity.GetTagItem(OrcaCoreDiagnostics.ExecutionModeKey)?.ToString(),
                OrcaCoreDiagnostics.EphemeralExecutionMode,
                StringComparison.Ordinal)).Should().BeTrue();
        measurements.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.CommandsProcessed && measurement.Value == 1);
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var statistics = engine.GetOperatorStatistics();
        statistics.Groups.Should().ContainSingle(group =>
            group.DefinitionId.Equals(definition.DefinitionId) &&
            group.DefinitionVersion.Equals(definition.DefinitionVersion) &&
            group.Status == global::OrcaCore.WorkflowInstanceStatus.Completed &&
            group.Count == 1);
        statistics.ActiveInstanceCount.Should().Be(0);
    }

    private sealed class TestState;

}
