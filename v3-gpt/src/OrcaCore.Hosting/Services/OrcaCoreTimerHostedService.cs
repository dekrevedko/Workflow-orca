using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts timer processing for applications that opt in.
/// </summary>
public sealed class OrcaCoreTimerHostedService(
    ITimerScheduler scheduler,
    DurableCommandProcessor commandProcessor,
    IOptions<OrcaCoreHostedServiceOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        await RunOnceAsync(value, stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(value.TimerSweepInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RunOnceAsync(value, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(
        OrcaCoreHostedServiceOptions value,
        CancellationToken stoppingToken)
    {
        var due = await scheduler
            .ClaimDueAsync(timeProvider.GetUtcNow(), value.TimerSweepBatchSize, stoppingToken)
            .ConfigureAwait(false);
        foreach (var command in due)
        {
            await commandProcessor.ProcessAsync(command, stoppingToken).ConfigureAwait(false);
        }
    }
}
