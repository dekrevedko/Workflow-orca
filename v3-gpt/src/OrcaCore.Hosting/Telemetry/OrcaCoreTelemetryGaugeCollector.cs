using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Hosting.Telemetry;

internal sealed class OrcaCoreTelemetryGaugeCollector(
    IWorkflowProjectionStore projectionStore,
    OrcaCoreTelemetryInstruments instruments,
    OrcaCoreOpenTelemetryOptions options,
    ILogger<OrcaCoreTelemetryGaugeCollector> logger) : BackgroundService
{
    private readonly TimeSpan interval = NormalizeInterval(options.GaugeCollectionInterval);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CollectOnceAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await CollectOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task CollectOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await projectionStore
                .GetStatisticsAsync(WorkflowProjectionQuery.All, cancellationToken)
                .ConfigureAwait(false);
            instruments.UpdateActiveInstances(statistics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to collect OrcaCore active instance gauge measurements.");
        }
    }

    private static TimeSpan NormalizeInterval(TimeSpan configured)
    {
        return configured > TimeSpan.Zero ? configured : TimeSpan.FromSeconds(15);
    }
}
