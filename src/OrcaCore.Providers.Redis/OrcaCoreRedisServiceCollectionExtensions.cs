using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrcaCore.Abstractions.Providers;
using StackExchange.Redis;

namespace OrcaCore.Providers.Redis;

/// <summary>
/// Provides Microsoft DI registration helpers for Redis projection providers.
/// </summary>
public static class OrcaCoreRedisServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Redis projection cache from an existing StackExchange.Redis database adapter.
    /// </summary>
    public static IServiceCollection AddOrcaCoreRedisProjectionCache(
        this IServiceCollection services,
        IDatabase database)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(database);

        services.TryAddSingleton(new RedisProjectionStore(database));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<RedisProjectionStore>()));
        return services;
    }

    /// <summary>
    /// Registers a host-created Redis projection store.
    /// </summary>
    public static IServiceCollection AddOrcaCoreRedisProjectionCache(
        this IServiceCollection services,
        RedisProjectionStore store)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(store);

        services.TryAddSingleton(store);
        services.Replace(ServiceDescriptor.Singleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<RedisProjectionStore>()));
        return services;
    }
}
