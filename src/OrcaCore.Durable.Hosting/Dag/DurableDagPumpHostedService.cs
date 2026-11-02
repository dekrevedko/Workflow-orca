using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed class DurableDagPumpHostedService(DurableDagChildPump children, DurableDagReadyPump ready,
    IWorkflowEventStore store, IOptions<DurableHostedServiceOptions> options, TimeProvider time,
    ILogger<DurableDagPumpHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan ObserverMaintenanceInterval = TimeSpan.FromSeconds(10);
    private const int ObserverMaintenanceBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configuration = options.Value;
        configuration.Validate();
        var boundary = new HostedServiceFailureBoundary(nameof(DurableDagPumpHostedService), logger, time);
        await Task.WhenAll(
            LoopAsync(configuration.OutboxPumpInterval, async token =>
            {
                var request = new OutboxClaimRequest(configuration.OutboxPumpBatchSize, time.GetUtcNow(), configuration.OutboxClaimLeaseDuration);
                await children.PumpOnceAsync(request, token).ConfigureAwait(false);
            }),
            LoopAsync(configuration.OutboxPumpInterval, async token =>
            {
                var request = new OutboxClaimRequest(configuration.OutboxPumpBatchSize, time.GetUtcNow(), configuration.OutboxClaimLeaseDuration);
                await ready.PumpOnceAsync(request, token).ConfigureAwait(false);
            }),
            LoopAsync(ObserverMaintenanceInterval, async token =>
            {
                while (await store.PruneExpiredDagRunObserversAsync(ObserverMaintenanceBatchSize, token).ConfigureAwait(false)
                       == ObserverMaintenanceBatchSize)
                    token.ThrowIfCancellationRequested();
            })).ConfigureAwait(false);

        async Task LoopAsync(TimeSpan interval, Func<CancellationToken, Task> operation)
        {
            using var timer = new PeriodicTimer(interval, time);
            do
            {
                await boundary.RunAsync(operation, configuration.TransientFailureBackoff, stoppingToken).ConfigureAwait(false);
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
    }
}
