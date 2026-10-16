using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Hosting.Telemetry;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class DurableDriverTelemetryAndOptionsTests
{
    [Fact]
    [Trait("AC", "DR-AC-031")]
    public void DriverBacklogGauges_SeparateContinuationAndExternalStates()
    {
        var measurements = new List<GaugeMeasurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName &&
                    instrument.Name is OrcaCoreMetrics.ContinuationPendingCountName or
                        OrcaCoreMetrics.OutboxExternalPendingCountName)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            measurements.Add(new GaugeMeasurement(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value)));
        });
        listener.Start();

        var instruments = new OrcaCoreTelemetryInstruments();
        instruments.UpdateFleetGauges(
            new WorkflowStatistics
            {
                Groups = [],
                Pressure = new WorkflowPressureMetrics
                {
                    ContinuationPendingCount = 5,
                    ContinuationRetryableCount = 3,
                    ContinuationClaimedCount = 2,
                    ExternalOutboxPendingCount = 11,
                    ExternalOutboxRetryableCount = 7,
                    ExternalOutboxClaimedCount = 4
                }
            },
            [],
            [],
            "PostgreSql");

        listener.RecordObservableInstruments();

        GaugeValues(measurements, OrcaCoreMetrics.ContinuationPendingCountName, "PostgreSql")
            .Should().BeEquivalentTo(new Dictionary<string, long>
            {
                ["pending"] = 5,
                ["retryable"] = 3,
                ["claimed"] = 2
            });
        GaugeValues(measurements, OrcaCoreMetrics.OutboxExternalPendingCountName, "PostgreSql")
            .Should().BeEquivalentTo(new Dictionary<string, long>
            {
                ["pending"] = 11,
                ["retryable"] = 7,
                ["claimed"] = 4
            });
    }

    [Fact]
    [Trait("AC", "DR-AC-031")]
    public void HostedDriverOptions_RejectInvalidValuesAndReachResolvedServices()
    {
        foreach (var invalid in InvalidDriverOptions())
        {
            Action validate = invalid.Validate;
            validate.Should().Throw<InvalidOperationException>();
        }

        var services = new ServiceCollection();
        services.AddOrcaCore();
        services.AddOrcaCoreHostedServices(options =>
        {
            options.ContinuationPumpInterval = TimeSpan.FromSeconds(2);
            options.ContinuationPumpBatchSize = 17;
            options.ContinuationClaimLeaseDuration = TimeSpan.FromSeconds(23);
            options.ContinuationDrainTimeout = TimeSpan.FromSeconds(29);
            options.ContinuationWorkerConcurrency = 3;
            options.ContinuationMaxDriveAttemptsBeforePark = 4;
            options.ContinuationInitialFailureBackoff = TimeSpan.FromSeconds(5);
            options.MaxCommandsPerSegment = 31;
            options.MaxSegmentDuration = TimeSpan.FromSeconds(37);
        });
        using var provider = services.BuildServiceProvider();

        var configured = provider.GetRequiredService<IOptions<OrcaCoreHostedServiceOptions>>().Value;
        configured.Validate();
        configured.ContinuationWorkerConcurrency.Should().Be(3);
        configured.ContinuationMaxDriveAttemptsBeforePark.Should().Be(4);
        configured.ContinuationInitialFailureBackoff.Should().Be(TimeSpan.FromSeconds(5));
        configured.MaxCommandsPerSegment.Should().Be(31);
        configured.MaxSegmentDuration.Should().Be(TimeSpan.FromSeconds(37));
        ResolveInternalService(services, provider, "OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime")
            .Should().NotBeNull();
        ResolveInternalService(services, provider, "OrcaCore.Engine.Durable.Driver.DurableContinuationPump")
            .Should().NotBeNull();
    }

    private static IReadOnlyList<OrcaCoreHostedServiceOptions> InvalidDriverOptions()
    {
        return
        [
            new() { ContinuationPumpInterval = TimeSpan.Zero },
            new() { ContinuationPumpBatchSize = 0 },
            new() { ContinuationClaimLeaseDuration = TimeSpan.Zero },
            new() { ContinuationDrainTimeout = TimeSpan.Zero },
            new() { ContinuationWorkerConcurrency = 0 },
            new() { ContinuationMaxDriveAttemptsBeforePark = 0 },
            new() { ContinuationInitialFailureBackoff = TimeSpan.Zero },
            new() { MaxCommandsPerSegment = 0 },
            new() { MaxSegmentDuration = TimeSpan.Zero }
        ];
    }

    private static Dictionary<string, long> GaugeValues(
        IEnumerable<GaugeMeasurement> measurements,
        string instrumentName,
        string providerName)
    {
        return measurements
            .Where(measurement =>
                measurement.InstrumentName == instrumentName &&
                Equals(measurement.Tags.GetValueOrDefault(OrcaCoreDiagnostics.ProviderNameKey), providerName))
            .ToDictionary(
                measurement => (string)measurement.Tags[OrcaCoreDiagnostics.OutboxStateKey]!,
                measurement => measurement.Value);
    }

    private static object ResolveInternalService(
        IServiceCollection services,
        IServiceProvider provider,
        string serviceTypeName)
    {
        var descriptor = services.Single(candidate =>
            string.Equals(candidate.ServiceType.FullName, serviceTypeName, StringComparison.Ordinal));
        return provider.GetRequiredService(descriptor.ServiceType);
    }

    private sealed record GaugeMeasurement(
        string InstrumentName,
        long Value,
        IReadOnlyDictionary<string, object?> Tags);
}
