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
