using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.SqlServer.Internal;

namespace OrcaCore.Providers.SqlServer;

/// <summary>Programmatic options for the production SQL Server durable provider role.</summary>
public sealed class SqlServerDurableProviderOptions
{
    /// <summary>Creates one immutable provider profile.</summary>
    public SqlServerDurableProviderOptions(string connectionString, string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ConnectionString = connectionString;
        Schema = schema;
    }

    /// <summary>Gets the SQL Server connection string.</summary>
    public string ConnectionString { get; }

    /// <summary>Gets the provider-owned schema.</summary>
    public string Schema { get; }
}

/// <summary>Registers the production SQL Server durable provider role.</summary>
public static class OrcaCoreSqlServerProviderServiceCollectionExtensions
{
    private const string ProviderRoleName = "sqlserver";

    /// <summary>Registers one complete certified SQL Server durable provider role.</summary>
    public static IServiceCollection AddOrcaCoreSqlServerDurableProvider(
        this IServiceCollection services,
        SqlServerDurableProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Schema);

        var frozenConnectionString = new SqlConnectionStringBuilder(options.ConnectionString).ConnectionString;
        var frozenSchema = options.Schema;
        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDurableProviderRole))
            .Select(descriptor => descriptor.ImplementationInstance as IDurableProviderRole)
            .FirstOrDefault(role => role is not null);
        if (existing is SqlServerDurableProviderRole sqlServer)
        {
            if (string.Equals(sqlServer.ConnectionString, frozenConnectionString, StringComparison.Ordinal) &&
                string.Equals(sqlServer.Schema, frozenSchema, StringComparison.Ordinal))
            {
                return services;
            }

            throw new InvalidOperationException(
                "The SQL Server durable provider role is already registered with different options.");
        }

        if (existing is not null)
        {
            throw new InvalidOperationException($"Durable provider role '{existing.Name}' is already registered.");
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
                "The SQL Server durable provider role cannot be combined with pre-registered provider ports: " +
                string.Join(", ", preRegisteredPorts));
        }

        services.TryAddSingleton(provider => new SqlServerStateDocumentStore(
            frozenConnectionString,
            frozenSchema));
        services.TryAddSingleton(provider => new SqlServerWorkflowStore(
            provider.GetRequiredService<SqlServerStateDocumentStore>(),
            provider.GetService<TimeProvider>() ?? TimeProvider.System));
        services.TryAddSingleton(provider => new SqlServerResourcePoolStore(
            provider.GetRequiredService<SqlServerStateDocumentStore>()));
        services.TryAddSingleton(provider => new SqlServerResourceGovernanceStore(
            provider.GetRequiredService<SqlServerStateDocumentStore>()));
        services.TryAddSingleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowStartIdempotencyStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowOperationalStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IWorkflowProviderMaintenanceStore>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<SqlServerWorkflowStore>());
        services.TryAddSingleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<SqlServerResourcePoolStore>());
        services.TryAddSingleton<IDurableResourceGovernanceStore>(provider =>
            provider.GetRequiredService<SqlServerResourceGovernanceStore>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, SqlServerProviderInitializationHostedService>());
        services.AddSingleton<IDurableProviderRole>(
            new SqlServerDurableProviderRole(frozenConnectionString, frozenSchema));
        return services;
    }

    private sealed record SqlServerDurableProviderRole(
        string ConnectionString,
        string Schema) : IDurableProviderRole
    {
        public string Name => ProviderRoleName;

        public bool IsDevelopmentOnly => false;
    }
}
