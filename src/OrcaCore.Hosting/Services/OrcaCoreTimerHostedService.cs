using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    TimeProvider timeProvider,
    ILogger<OrcaCoreTimerHostedService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var value = options.Value;
        value.Validate();

        var failureBoundary = new HostedServiceFailureBoundary(
            nameof(OrcaCoreTimerHostedService),
            logger,
            timeProvider);

        await failureBoundary
            .RunAsync(token => RunOnceAsync(value, token), value.TransientFailureBackoff, stoppingToken)
            .ConfigureAwait(false);
        using var timer = new PeriodicTimer(value.TimerSweepInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await failureBoundary
                .RunAsync(token => RunOnceAsync(value, token), value.TransientFailureBackoff, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(
        OrcaCoreHostedServiceOptions value,
        CancellationToken stoppingToken)
    {
        var now = timeProvider.GetUtcNow();
        var due = await scheduler
            .ClaimDueAsync(
                new TimerClaimRequest(
                    now,
                    value.TimerSweepBatchSize,
                    now,
                    value.TimerClaimLeaseDuration),
                stoppingToken)
            .ConfigureAwait(false);
        foreach (var command in due)
        {
            try
            {
                var result = await commandProcessor.ProcessAsync(command, stoppingToken).ConfigureAwait(false);
                if (ShouldCompleteClaim(result.Outcome))
                {
                    await scheduler.CompleteAsync(command.TimerId, stoppingToken).ConfigureAwait(false);
                }
                else
                {
                    await scheduler.ReleaseAsync(command.TimerId, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                await scheduler.ReleaseAsync(command.TimerId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception)
            {
                await scheduler.ReleaseAsync(command.TimerId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
    }

    private static bool ShouldCompleteClaim(DurableCommandOutcome outcome)
    {
        return outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.NoOp;
    }
}
