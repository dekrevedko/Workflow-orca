using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Driver;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts the durable lane driver's restart-safety loop (DR-033/DR-034): claims internal
/// <c>continue</c> outbox records and advances the referenced instances. On shutdown the loop
/// stops claiming immediately; the in-flight batch finishes its current commits inside a
/// bounded drain window instead of being torn mid-advancement.
/// </summary>
public sealed class OrcaCoreContinuationPumpHostedService(
    DurableContinuationPump pump,
    IOptions<OrcaCoreHostedServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<OrcaCoreContinuationPumpHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        var failureBoundary = new HostedServiceFailureBoundary(
            nameof(OrcaCoreContinuationPumpHostedService),
            logger,
            timeProvider);

        using var drainCancellation = new CancellationTokenSource();
        ITimer? drainTimer = null;
        try
        {
            using var stopRegistration = stoppingToken.Register(() =>
                drainTimer = timeProvider.CreateTimer(
                    _ => drainCancellation.Cancel(),
                    null,
                    value.ContinuationDrainTimeout,
                    Timeout.InfiniteTimeSpan));

            await failureBoundary
                .RunAsync(
                    _ => RunOnceAsync(value, drainCancellation.Token),
                    value.TransientFailureBackoff,
                    stoppingToken)
                .ConfigureAwait(false);
            using var timer = new PeriodicTimer(value.ContinuationPumpInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await failureBoundary
                    .RunAsync(
                        _ => RunOnceAsync(value, drainCancellation.Token),
                        value.TransientFailureBackoff,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            drainTimer?.Dispose();
        }
    }

    private Task RunOnceAsync(
        OrcaCoreHostedServiceOptions value,
        CancellationToken drainToken)
    {
        return pump.PumpOnceAsync(
            new OutboxClaimRequest(
                value.ContinuationPumpBatchSize,
                timeProvider.GetUtcNow(),
                value.ContinuationClaimLeaseDuration),
            drainToken);
    }
}
