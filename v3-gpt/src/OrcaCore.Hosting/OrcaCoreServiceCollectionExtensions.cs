using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.RabbitMq;

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
        services.TryAddSingleton<DurableCommandProcessor>();
        services.TryAddSingleton(provider => new DurableManagement(
            provider.GetRequiredService<IWorkflowProjectionStore>(),
            provider.GetRequiredService<IResourcePoolStore>(),
            provider.GetRequiredService<IWorkflowEventStore>()));

        return services;
    }

    /// <summary>
    /// Registers RabbitMQ as the durable outbox message dispatcher.
    /// </summary>
    public static IServiceCollection AddOrcaCoreRabbitMq(
        this IServiceCollection services,
        RabbitMqMessageDispatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddSingleton<IRabbitMqPublisher, RabbitMqClientPublisher>();
        services.AddSingleton<IMessageDispatcher, RabbitMqMessageDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers OrcaCore background services for hosted applications.
    /// </summary>
    public static IServiceCollection AddOrcaCoreHostedServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IHostedService, OrcaCoreOutboxPumpHostedService>();
        services.AddSingleton<IHostedService, OrcaCoreTimerHostedService>();
        services.AddSingleton<IHostedService, OrcaCoreOperationalSweepHostedService>();
        return services;
    }
}
