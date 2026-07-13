using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Provides Microsoft DI registration helpers for PostgreSQL durable providers.
/// </summary>
public static class OrcaCorePostgreSqlServiceCollectionExtensions
{
    /// <summary>
    /// Registers PostgreSQL durable stores for workflow events, projections, inbox, outbox, timers, retention, and resource pools.
    /// </summary>
    public static IServiceCollection AddOrcaCorePostgreSql(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.TryAddSingleton<PostgreSqlWorkflowStore>();
        services.TryAddSingleton<PostgreSqlResourcePoolStore>();
        RegisterWorkflowPorts(services);
        services.Replace(ServiceDescriptor.Singleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<PostgreSqlResourcePoolStore>()));
        return services;
    }

    /// <summary>
    /// Registers PostgreSQL durable stores from an existing data source owned by the host.
    /// </summary>
    public static IServiceCollection AddOrcaCorePostgreSql(
        this IServiceCollection services,
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataSource);

        services.TryAddSingleton(dataSource);
        services.TryAddSingleton<PostgreSqlWorkflowStore>();
        services.TryAddSingleton<PostgreSqlResourcePoolStore>();
        RegisterWorkflowPorts(services);
        services.Replace(ServiceDescriptor.Singleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<PostgreSqlResourcePoolStore>()));
        return services;
    }

    private static void RegisterWorkflowPorts(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowStartIdempotencyStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowRetentionStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>()));
    }
}
