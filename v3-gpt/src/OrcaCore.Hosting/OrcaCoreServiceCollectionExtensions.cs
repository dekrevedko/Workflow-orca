using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting.Services;
using OrcaCore.Hosting.Telemetry;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Hosting;

/// <summary>
/// Provides explicit Microsoft DI registration helpers for OrcaCore.
/// </summary>
public static class OrcaCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers OrcaCore engines and in-memory provider defaults.
    /// </summary>
    public static IServiceCollection AddOrcaCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.TryAddSingleton<EphemeralWorkflowEngine>();
        services.TryAddSingleton<InMemoryWorkflowProvider>();
        services.TryAddSingleton<InMemoryResourcePoolStore>();
        services.TryAddSingleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IMessageDispatcher>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowPayloadSerializer>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<InMemoryResourcePoolStore>());
        services.TryAddSingleton<OrcaCoreTelemetryObserver>();
        services.TryAddSingleton<IWorkflowRuntimeObserver>(provider =>
            provider.GetRequiredService<OrcaCoreTelemetryObserver>());
        services.TryAddSingleton<IOutboxPumpObserver>(provider =>
            provider.GetRequiredService<OrcaCoreTelemetryObserver>());
        services.TryAddSingleton(provider => new DurableCommandRuntime(
            provider.GetRequiredService<IWorkflowEventStore>(),
            provider.GetService<IResourcePoolStore>()));
        services.TryAddSingleton(provider => new DurableCommandProcessor(
            provider.GetRequiredService<DurableCommandRuntime>(),
            provider.GetService<IWorkflowRuntimeObserver>()));
        services.TryAddSingleton(provider => new DurableOutboxPump(
            provider.GetRequiredService<IWorkflowOutboxStore>(),
            provider.GetRequiredService<IMessageDispatcher>(),
            provider.GetService<IOutboxPumpObserver>()));
        services.TryAddSingleton(provider => new DurableManagement(
            provider.GetRequiredService<IWorkflowProjectionStore>(),
            provider.GetRequiredService<IResourcePoolStore>(),
            provider.GetRequiredService<IWorkflowEventStore>(),
            provider.GetRequiredService<DurableCommandProcessor>()));

        return services;
    }

    /// <summary>
    /// Registers OrcaCore background services for hosted applications.
    /// </summary>
    public static IServiceCollection AddOrcaCoreHostedServices(
        this IServiceCollection services,
        Action<OrcaCoreHostedServiceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OrcaCoreHostedServiceOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrcaCoreOutboxPumpHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrcaCoreTimerHostedService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrcaCoreOperationalSweepHostedService>());
        return services;
    }
}
