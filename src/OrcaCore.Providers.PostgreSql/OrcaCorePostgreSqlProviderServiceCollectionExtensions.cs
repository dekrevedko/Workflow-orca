using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>Programmatic options for the production PostgreSQL durable provider role.</summary>
public sealed class PostgreSqlDurableProviderOptions
{
    /// <summary>Creates one immutable provider profile.</summary>
    public PostgreSqlDurableProviderOptions(string connectionString, string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ConnectionString = connectionString;
        Schema = schema;
    }

    /// <summary>Gets the PostgreSQL connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Gets the provider-owned schema.</summary>
    public string Schema { get; }
}

/// <summary>Registers the production PostgreSQL durable provider role.</summary>
public static class OrcaCorePostgreSqlProviderServiceCollectionExtensions
{
    /// <summary>Registers one complete certified PostgreSQL durable provider role.</summary>
    public static IServiceCollection AddOrcaCorePostgreSqlDurableProvider(
        this IServiceCollection services,
        PostgreSqlDurableProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Schema);
        var copiedConnectionString = options.ConnectionString;
        var copiedSchema = options.Schema;

        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDurableProviderRole))
            .Select(descriptor => descriptor.ImplementationInstance as IDurableProviderRole)
            .FirstOrDefault(role => role is not null);
        if (existing is PostgreSqlDurableProviderRole postgres)
        {
            if (string.Equals(postgres.ConnectionString, copiedConnectionString, StringComparison.Ordinal) &&
                string.Equals(postgres.Schema, copiedSchema, StringComparison.Ordinal))
            {
                return services;
            }

            throw new InvalidOperationException(
                "The PostgreSQL durable provider role is already registered with different options.");
        }

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Durable provider role '{existing.Name}' is already registered.");
        }

        var providerPorts = new[]
        {
            typeof(IWorkflowEventStore),
            typeof(IWorkflowInboxStore),
            typeof(IWorkflowStartIdempotencyStore),
            typeof(IWorkflowOutboxStore),
            typeof(IWorkflowProjectionStore),
            typeof(IWorkflowOperationalStore),
            typeof(IWorkflowProviderMaintenanceStore),
            typeof(ITimerScheduler),
            typeof(IResourcePoolStore),
            typeof(IDurableResourceGovernanceStore)
        };
        var preRegisteredPorts = services
            .Where(descriptor => providerPorts.Contains(descriptor.ServiceType))
            .Select(descriptor => descriptor.ServiceType.FullName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (preRegisteredPorts.Length != 0)
        {
            throw new InvalidOperationException(
                "The PostgreSQL durable provider role cannot be combined with pre-registered " +
                $"provider ports: {string.Join(", ", preRegisteredPorts)}.");
        }

        var builder = new NpgsqlConnectionStringBuilder(copiedConnectionString)
        {
            SearchPath = copiedSchema
        };
        var frozenConnectionString = builder.ConnectionString;
        services.TryAddSingleton(_ => NpgsqlDataSource.Create(frozenConnectionString));
        services.TryAddSingleton<PostgreSqlWorkflowStore>();
        services.TryAddSingleton<PostgreSqlResourcePoolStore>();
        services.TryAddSingleton<PostgreSqlResourceGovernanceStore>();
        services.TryAddSingleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowStartIdempotencyStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowOperationalStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IWorkflowProviderMaintenanceStore>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<PostgreSqlWorkflowStore>());
        services.TryAddSingleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<PostgreSqlResourcePoolStore>());
        services.TryAddSingleton<IDurableResourceGovernanceStore>(provider =>
            provider.GetRequiredService<PostgreSqlResourceGovernanceStore>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, PostgreSqlProviderInitializationHostedService>());
        services.AddSingleton<IDurableProviderRole>(
            new PostgreSqlDurableProviderRole(copiedConnectionString, copiedSchema));
        return services;
    }

    private sealed record PostgreSqlDurableProviderRole(
        string ConnectionString,
        string Schema) : IDurableProviderRole
    {
        public string Name => "postgresql";

        public bool IsDevelopmentOnly => false;
    }
}
