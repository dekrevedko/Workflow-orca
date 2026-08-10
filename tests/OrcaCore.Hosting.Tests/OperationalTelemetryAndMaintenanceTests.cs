using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class OperationalTelemetryAndMaintenanceTests
{
    [Fact]
    public void DurableOperationalPolicy_RejectsNonpositiveOrUnrepresentableThresholds()
    {
        var options = new DurableHostedServiceOptions { StuckDetectionThreshold = TimeSpan.Zero };

        Action validateOptions = options.Validate;
        Action rejectZero = () => new WorkflowOperatorStatisticsRequest(DateTimeOffset.UnixEpoch, TimeSpan.Zero);
        Action rejectUnderflow = () => new WorkflowOperatorStatisticsRequest(
            DateTimeOffset.MinValue,
            TimeSpan.FromTicks(1));

        validateOptions.Should().Throw<InvalidOperationException>()
            .WithMessage("*stuck-detection threshold*");
        rejectZero.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("stuckThreshold");
        rejectUnderflow.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("stuckThreshold");
    }

    [Fact]
    public void DurableHost_UsesProviderOwnedOperationalAndMaintenancePorts()
    {
        var services = CreateServices();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IWorkflowOperationalStore>().Should().NotBeNull();
        provider.GetRequiredService<IWorkflowProviderMaintenanceStore>().Should().NotBeNull();
        provider.GetServices<IHostedService>()
            .Should().Contain(service => service.GetType().Name == "OrcaCoreOperationalSweepHostedService");
    }

    [Fact]
    public async Task OperationalSweep_RefreshesBclGaugesFromTheProviderSnapshot()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var observed = new ConcurrentQueue<(string Name, long Value, string? Provider)>();
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
            observed.Enqueue((
                instrument.Name,
                value,
                tags.ToArray().SingleOrDefault(tag => tag.Key == OrcaCoreDiagnostics.ProviderNameKey).Value as string)));
        listener.Start();

        await OrcaCoreOperationalSweepHostedService.CollectOnceAsync(
            provider.GetRequiredService<IResourcePoolStore>(),
            provider.GetRequiredService<IWorkflowOperationalStore>(),
            TimeProvider.System,
            TestContext.Current.CancellationToken);
        listener.RecordObservableInstruments();

        observed.Should().Contain(measurement =>
            measurement.Name == OrcaCoreMetrics.StreamEvents &&
            measurement.Value == 0 &&
            measurement.Provider == "in-memory");
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create("operational-telemetry-tests"),
                Pools = []
            }
        });
        return services;
    }
}
