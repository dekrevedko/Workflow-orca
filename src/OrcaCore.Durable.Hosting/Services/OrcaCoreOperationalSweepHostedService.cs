using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        await CollectOnceAsync(
            resourcePoolStore,
            operationalStore,
            leaseDiagnostics,
            timeProvider,
            value.StuckDetectionThreshold,
            stoppingToken).ConfigureAwait(false);
    }

    internal static async Task CollectOnceAsync(
        IResourcePoolStore resourcePools,
        IWorkflowOperationalStore operations,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        await CollectOnceAsync(
            resourcePools,
            operations,
            leaseDiagnostics: null,
            clock,
            DurableOperationalDefaults.StuckDetectionThreshold,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task CollectOnceAsync(
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

        var statistics = await operations
            .GetOperatorStatisticsAsync(
                new WorkflowOperatorStatisticsRequest(clock.GetUtcNow(), stuckDetectionThreshold),
                cancellationToken)
            .ConfigureAwait(false);
        var pools = await resourcePools.ListPoolsAsync(cancellationToken).ConfigureAwait(false);
        OrcaCoreDurableDiagnostics.RefreshOperatorStatistics(statistics, pools);
    }
}
