using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Diagnostics;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Diagnostics;

public sealed class EphemeralDiagnosticsTests
{
    [Fact]
    public async Task Start_EmitsActivityAndCounter()
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
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(() => new CompleteStep())
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        activities.Where(activity =>
                activity.OperationName == "orcacore.ephemeral.start" &&
                string.Equals(
                    activity.GetTagItem("workflow.status")?.ToString(),
                    "Completed",
                    StringComparison.Ordinal))
            .Should().NotBeEmpty();
        measurements.Should().Contain(measurement =>
            measurement.Name == "orcacore.ephemeral.workflows.started" && measurement.Value == 1);
    }

    private sealed class TestState;

    private sealed class CompleteStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
