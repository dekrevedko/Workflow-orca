using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.RabbitMq;
using OrcaCore.SampleHost;

namespace OrcaCore.Integration.Tests.Support;

internal sealed class RecordingMessageDispatcher : IMessageDispatcher
{
    private readonly TaskCompletionSource<OutboxWrite> dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<OutboxWrite> records = [];

    internal Task<OutboxWrite> Dispatched => dispatched.Task;

    internal IReadOnlyList<OutboxWrite> Records => records;

    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.Add(record);
        dispatched.TrySetResult(record);
        return Task.FromResult(DispatchResult.Success);
    }
}

internal static class OrcaIntegrationHost
{
    internal static async Task<(IHost Host, RecordingMessageDispatcher Dispatcher, FakeTimeProvider Clock)> BuildPostgreSqlAsync(
        string connectionString,
        Action<OrcaCoreHostedServiceOptions>? configure = null,
        CancellationToken cancellationToken = default)
    {
        var clock = new FakeTimeProvider(IntegrationIds.Timestamp(0));
        var dispatcher = new RecordingMessageDispatcher();
        var host = Build(connectionString, clock, dispatcher, configure);
        await host.StartAsync(cancellationToken);
        return (host, dispatcher, clock);
    }

    internal static IHost Build(
        string connectionString,
        FakeTimeProvider clock,
        IMessageDispatcher dispatcher,
        Action<OrcaCoreHostedServiceOptions>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services
            .AddOrcaCore()
            .AddOrcaCorePostgreSql(connectionString)
            .AddOrcaCoreHostedServices(options =>
            {
                options.OutboxPumpInterval = TimeSpan.FromMinutes(5);
                options.TimerSweepInterval = TimeSpan.FromMinutes(5);
                options.OperationalSweepInterval = TimeSpan.FromMinutes(5);
                configure?.Invoke(options);
            });
        builder.Services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher>(dispatcher));
        return builder.Build();
    }

    internal static IHost BuildSampleHost()
    {
        return SampleHostApplication.Build([]);
    }

    internal static async Task PumpOutboxOnceAsync(IHost host, CancellationToken cancellationToken)
    {
        var pump = host.Services.GetRequiredService<DurableOutboxPump>();
        await pump.PumpOnceAsync(100, cancellationToken);
    }

    internal static async Task FireTimersOnceAsync(IHost host, CancellationToken cancellationToken)
    {
        var scheduler = host.Services.GetRequiredService<ITimerScheduler>();
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        var clock = host.Services.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();
        var due = await scheduler.ClaimDueAsync(
            new TimerClaimRequest(now, 100, now, TimeSpan.FromMinutes(5)),
            cancellationToken);
        foreach (var command in due)
        {
            var result = await processor.ProcessAsync(command, cancellationToken);
            if (result.Outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.NoOp)
            {
                await scheduler.CompleteAsync(command.TimerId, cancellationToken);
            }
            else
            {
                await scheduler.ReleaseAsync(command.TimerId, cancellationToken);
            }
        }
    }

    internal static async Task RunOperationalSweepOnceAsync(IHost host, CancellationToken cancellationToken)
    {
        foreach (var service in host.Services.GetServices<IHostedService>())
        {
            if (service is OrcaCoreOperationalSweepHostedService)
            {
                await service.StartAsync(cancellationToken);
                await service.StopAsync(cancellationToken);
                return;
            }
        }
    }
}
