using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Driver;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts periodic OrcaCore operational sweeps.
/// </summary>
internal sealed class OrcaCoreOperationalSweepHostedService(
    IResourcePoolStore resourcePoolStore,
    IWorkflowOperationalStore operationalStore,
    DurableResourceLeaseDiagnostics leaseDiagnostics,
    IOptions<DurableHostedServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<OrcaCoreOperationalSweepHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        var failureBoundary = new HostedServiceFailureBoundary(
            nameof(OrcaCoreOperationalSweepHostedService),
            logger,
            timeProvider);

        await failureBoundary
            .RunAsync(RunOnceAsync, value.TransientFailureBackoff, stoppingToken)
            .ConfigureAwait(false);

        using var timer = new PeriodicTimer(value.OperationalSweepInterval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await failureBoundary
                .RunAsync(RunOnceAsync, value.TransientFailureBackoff, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        var stopwatch = Stopwatch.StartNew();
        var snapshot = await CollectSnapshotAsync(
            resourcePoolStore,
            operationalStore,
            leaseDiagnostics,
            timeProvider,
            value.StuckDetectionThreshold,
            stoppingToken).ConfigureAwait(false);
        stopwatch.Stop();

        using var providerScope = logger.BeginScope(new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.ProviderNameKey] = snapshot.Statistics.ProviderName
        });
        OperationalSweepLog.SweepCompleted(
            logger,
            snapshot.Statistics.ProviderName,
            snapshot.Statistics.Pressure.ActiveInstanceCount,
            snapshot.Statistics.Pressure.StuckInstanceCount,
            snapshot.Pools.Count,
            stopwatch.Elapsed.TotalMilliseconds);
        if (snapshot.Statistics.Pressure.StuckInstanceCount > 0)
        {
            OperationalSweepLog.StuckInstancesObserved(
                logger,
                snapshot.Statistics.ProviderName,
                snapshot.Statistics.Pressure.StuckInstanceCount,
                value.StuckDetectionThreshold.TotalMilliseconds);
        }

        foreach (var pool in snapshot.Pools)
        {
            using var poolScope = logger.BeginScope(new Dictionary<string, object?>
            {
                [OrcaCoreDiagnostics.ResourcePoolNameKey] = pool.Name
            });
            OperationalSweepLog.ResourcePoolObserved(
                logger,
                pool.Name,
                pool.Capacity,
                pool.AvailableCapacity,
                pool.HeldTickets.Count,
                pool.QueuedWaiters.Count);
        }
    }

    internal static async Task CollectOnceAsync(
        IResourcePoolStore resourcePools,
        IWorkflowOperationalStore operations,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        _ = await CollectSnapshotAsync(
            resourcePools,
            operations,
            leaseDiagnostics: null,
            clock,
            DurableOperationalDefaults.StuckDetectionThreshold,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationalSnapshot> CollectSnapshotAsync(
        IResourcePoolStore resourcePools,
        IWorkflowOperationalStore operations,
        DurableResourceLeaseDiagnostics? leaseDiagnostics,
        TimeProvider clock,
        TimeSpan stuckDetectionThreshold,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resourcePools);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(clock);
        _ = await resourcePools
            .ExpireTicketsAsync(clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        if (leaseDiagnostics is not null)
        {
            await foreach (var _ in leaseDiagnostics
                               .EnumerateOutstandingAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
            }
        }

        var statisticsRequest = new WorkflowOperatorStatisticsRequest(
            clock.GetUtcNow(),
            stuckDetectionThreshold);
        await operations
            .RefreshStuckStateAsync(statisticsRequest, cancellationToken)
            .ConfigureAwait(false);
        var statistics = await operations
            .GetOperatorStatisticsAsync(cancellationToken)
            .ConfigureAwait(false);
        var pools = await resourcePools.ListPoolsAsync(cancellationToken).ConfigureAwait(false);
        OrcaCoreDurableDiagnostics.RefreshOperatorStatistics(statistics, pools);
        return new OperationalSnapshot(statistics, pools);
    }

    private sealed record OperationalSnapshot(
        WorkflowOperatorStatistics Statistics,
        IReadOnlyList<ResourcePoolSnapshot> Pools);
}

internal static partial class OperationalSweepLog
{
    private const int StuckInstancesObservedEventId = 1501;
    private const int ResourcePoolObservedEventId = 1602;
    private const int SweepCompletedEventId = 1801;

    [LoggerMessage(
        EventId = StuckInstancesObservedEventId,
        Level = LogLevel.Warning,
        Message = "Operational snapshot for provider {ProviderName} contains {StuckCount} stuck instance(s) at threshold {ThresholdMs} ms.")]
    internal static partial void StuckInstancesObserved(
        ILogger logger,
        string providerName,
        long stuckCount,
        double thresholdMs);

    [LoggerMessage(
        EventId = ResourcePoolObservedEventId,
        Level = LogLevel.Information,
        Message = "Resource pool {PoolName} has capacity {Capacity}, available {AvailableCapacity}, {HeldTicketCount} held ticket(s), and {QueuedCount} queued request(s).")]
    internal static partial void ResourcePoolObserved(
        ILogger logger,
        string poolName,
        int capacity,
        int availableCapacity,
        int heldTicketCount,
        int queuedCount);

    [LoggerMessage(
        EventId = SweepCompletedEventId,
        Level = LogLevel.Information,
        Message = "Operational sweep for provider {ProviderName} completed with {ActiveCount} active instance(s), {StuckCount} stuck instance(s), and {PoolCount} resource pool(s) after {DurationMs} ms.")]
    internal static partial void SweepCompleted(
        ILogger logger,
        string providerName,
        long activeCount,
        long stuckCount,
        int poolCount,
        double durationMs);
}
