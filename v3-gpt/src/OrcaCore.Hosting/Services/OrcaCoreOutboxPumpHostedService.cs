using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts durable outbox pumping for applications that opt in.
/// </summary>
public sealed class OrcaCoreOutboxPumpHostedService(
    DurableOutboxPump pump,
    IOptions<OrcaCoreHostedServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<OrcaCoreOutboxPumpHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        var failureBoundary = new HostedServiceFailureBoundary(
            nameof(OrcaCoreOutboxPumpHostedService),
            logger,
            timeProvider);

        await failureBoundary
            .RunAsync(token => RunOnceAsync(value, token), value.TransientFailureBackoff, stoppingToken)
            .ConfigureAwait(false);
        using var timer = new PeriodicTimer(value.OutboxPumpInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await failureBoundary
                .RunAsync(token => RunOnceAsync(value, token), value.TransientFailureBackoff, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private Task RunOnceAsync(
        OrcaCoreHostedServiceOptions value,
        CancellationToken stoppingToken)
    {
        return pump.PumpOnceAsync(
            new OutboxClaimRequest(
                value.OutboxPumpBatchSize,
                timeProvider.GetUtcNow(),
                value.OutboxClaimLeaseDuration)
            {
                // DR-037: internal continuation records belong to the continuation pump; the
                // external dispatcher never delivers them to a transport.
                KindSelector = OutboxKindSelector.Excluding(OutboxKinds.Continue)
            },
            stoppingToken);
    }
}
