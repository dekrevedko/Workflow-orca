using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts durable outbox pumping for applications that opt in.
/// </summary>
public sealed class OrcaCoreOutboxPumpHostedService(
    DurableOutboxPump pump,
    IOptions<OrcaCoreHostedServiceOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        await RunOnceAsync(value, stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(value.OutboxPumpInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RunOnceAsync(value, stoppingToken).ConfigureAwait(false);
        }
    }

    private Task RunOnceAsync(
        OrcaCoreHostedServiceOptions value,
        CancellationToken stoppingToken)
    {
        return pump.PumpOnceAsync(value.OutboxPumpBatchSize, stoppingToken);
    }
}
