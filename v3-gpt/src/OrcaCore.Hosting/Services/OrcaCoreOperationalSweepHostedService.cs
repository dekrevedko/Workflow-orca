using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts periodic OrcaCore operational sweeps.
/// </summary>
public sealed class OrcaCoreOperationalSweepHostedService(
    IResourcePoolStore resourcePoolStore,
    IOptions<OrcaCoreHostedServiceOptions> options,
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

    private Task RunOnceAsync(CancellationToken stoppingToken)
    {
        return resourcePoolStore.ExpireTicketsAsync(timeProvider.GetUtcNow(), stoppingToken);
    }
}
