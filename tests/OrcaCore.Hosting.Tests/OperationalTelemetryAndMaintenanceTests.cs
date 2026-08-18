using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Diagnostics;
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

    [Fact]
    public void CanonicalStructuredLogs_ExposeStableEventIdsAndStructuredProperties()
    {
        var logger = new RecordingLogger();

        DurableTelemetryLog.CommandCompleted(logger, "command", "committed", "instance", 1, 1, 1);
        DurableTelemetryLog.OutboxPumpCompleted(logger, 1, 1, 1, 0, 0);
        DurableTelemetryLog.OutboxPermanentFailure(logger, "kind", "record", 2, 1);
        DurableTelemetryLog.OutboxException(logger, "kind", "record", 3, "exception", "summary");
        DurableTelemetryLog.StepTransition(
            logger,
            "step",
            "instance",
            "root/step",
            "operation-1",
            4,
            null,
            null);
        DurableTelemetryLog.WaitTransition(logger, "wait", "instance", "event", "correlation");
        DurableTelemetryLog.TimerTransition(logger, "timer", "instance", "timer-id", DateTimeOffset.UnixEpoch);
        DurableTelemetryLog.ProviderCommitCompleted(logger, "provider", 0, 1, "committed", 1);
        DurableTelemetryLog.ProviderCommitConflict(logger, "provider", 0, 1, 1);
        DurableTelemetryLog.ProviderCommitFailed(
            logger,
            "provider",
            0,
            1,
            "exception",
            "summary",
            new InvalidOperationException("failure"));
        OperationalSweepLog.StuckInstancesObserved(logger, "provider", 1, 1);
        DurableTelemetryLog.ResourcePoolTransition(
            logger,
            "resource",
            "instance",
            "pool",
            "owner",
            "obligation",
            "ticket",
            5);
        OperationalSweepLog.ResourcePoolObserved(logger, "pool", 2, 1, 1, 0);
        DurableTelemetryLog.LifecycleTransition(logger, "started", "instance", "event");
        OperationalSweepLog.SweepCompleted(logger, "provider", 1, 0, 1, 1);
        OrcaCoreHostedServiceLog.ProcessingCycleFailed(
            logger,
            "service",
            TimeSpan.FromSeconds(1),
            new InvalidOperationException("failure"));

        logger.Records.Select(record => record.EventId).Should().Equal(
            1001, 1101, 1102, 1103, 1201, 1301, 1302, 1401, 1402, 1403,
            1501, 1601, 1602, 1701, 1801, 1901);
        logger.Records.Should().OnlyContain(record => record.Properties.ContainsKey("{OriginalFormat}"));
        logger.Records.Single(record => record.EventId == 1201).Properties["StepPath"]
            .Should().Be("root/step");
        logger.Records.Single(record => record.EventId == 1201).Properties["StepOperationId"]
            .Should().Be("operation-1");
        logger.Records.Single(record => record.EventId == 1201).Properties["StepAttemptNumber"]
            .Should().Be(4);
        logger.Records.Single(record => record.EventId == 1102).Properties["Attempt"]
            .Should().Be(2);
        logger.Records.Single(record => record.EventId == 1601).Properties["LeaseObligationId"]
            .Should().Be("obligation");
        logger.Records.Single(record => record.EventId == 1601).Properties["TicketId"]
            .Should().Be("ticket");
        logger.Records.Single(record => record.EventId == 1601).Properties["OwnerGeneration"]
            .Should().Be(5L);
        logger.Records.Single(record => record.EventId == 1401).Properties["ExpectedStreamVersion"]
            .Should().Be(0L);
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

    private sealed class RecordingLogger : ILogger
    {
        internal List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            Records.Add(new LogRecord(eventId.Id, properties));
        }
    }

    private sealed record LogRecord(int EventId, IReadOnlyDictionary<string, object?> Properties);

    private sealed class NullScope : IDisposable
    {
        internal static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
