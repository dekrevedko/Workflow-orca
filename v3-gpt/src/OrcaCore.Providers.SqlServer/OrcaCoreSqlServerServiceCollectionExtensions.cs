using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer;

/// <summary>
/// Provides Microsoft DI registration helpers for SQL Server durable providers.
/// </summary>
public static class OrcaCoreSqlServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers SQL Server durable stores for workflow events, projections, inbox, outbox, timers, retention, and resource pools.
    /// </summary>
    public static IServiceCollection AddOrcaCoreSqlServer(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton(provider =>
            new SqlServerWorkflowStore(connectionString, provider.GetService<TimeProvider>()));
        RegisterWorkflowPorts(services);
        return services;
    }

    private static void RegisterWorkflowPorts(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowStartIdempotencyStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IWorkflowRetentionStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
        services.Replace(ServiceDescriptor.Singleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>()));
    }
}
