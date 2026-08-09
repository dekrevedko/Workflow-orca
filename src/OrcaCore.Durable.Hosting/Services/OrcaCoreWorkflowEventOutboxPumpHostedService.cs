using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Durable.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Hosting.Services;

internal sealed class OrcaCoreWorkflowEventOutboxPumpHostedService(
    IServiceProvider services,
    IOptions<DurableHostedServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<OrcaCoreWorkflowEventOutboxPumpHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatcher = services.GetService<IWorkflowEventDispatcher>();
        if (dispatcher is null)
        {
            return;
        }

        var pump = new WorkflowEventOutboxPump(
            services.GetRequiredService<IWorkflowOutboxStore>(),
            dispatcher,
            services.GetService<IOutboxPumpObserver>(),
            timeProvider);
        var value = options.Value;
        value.Validate();
        var failureBoundary = new HostedServiceFailureBoundary(
            nameof(OrcaCoreWorkflowEventOutboxPumpHostedService),
            logger,
            timeProvider);
        await failureBoundary
            .RunAsync(token => RunOnceAsync(pump, value, token), value.TransientFailureBackoff, stoppingToken)
            .ConfigureAwait(false);
        using var timer = new PeriodicTimer(value.OutboxPumpInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await failureBoundary
                .RunAsync(token => RunOnceAsync(pump, value, token), value.TransientFailureBackoff, stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private Task RunOnceAsync(
        WorkflowEventOutboxPump pump,
        DurableHostedServiceOptions value,
        CancellationToken stoppingToken) =>
        pump.PumpOnceAsync(
            new OutboxClaimRequest(
                value.OutboxPumpBatchSize,
                timeProvider.GetUtcNow(),
                value.OutboxClaimLeaseDuration),
            stoppingToken);
}
