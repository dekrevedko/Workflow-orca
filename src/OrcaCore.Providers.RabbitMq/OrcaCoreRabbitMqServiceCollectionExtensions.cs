using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.RabbitMq;

/// <summary>
/// Provides Microsoft DI registration helpers for the RabbitMQ dispatcher plugin.
/// </summary>
public static class OrcaCoreRabbitMqServiceCollectionExtensions
{
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
        services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher, RabbitMqMessageDispatcher>());
        return services;
    }
}
