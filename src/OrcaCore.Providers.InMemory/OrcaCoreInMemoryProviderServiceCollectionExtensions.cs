using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;

namespace OrcaCore.Providers.InMemory;

/// <summary>Registers the development/test in-memory durable provider role.</summary>
public static class OrcaCoreInMemoryProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers one complete, process-local durable provider role.
    /// </summary>
    public static IServiceCollection AddOrcaCoreInMemoryDurableProvider(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDurableProviderRole))
            .Select(descriptor => descriptor.ImplementationInstance as IDurableProviderRole)
            .FirstOrDefault(role => role is not null);
        if (existing is not null)
        {
            if (existing is InMemoryDurableProviderRole)
            {
                return services;
            }

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
                "The in-memory durable provider role cannot be combined with pre-registered " +
                $"provider ports: {string.Join(", ", preRegisteredPorts)}.");
        }

        services.TryAddSingleton<InMemoryWorkflowProvider>();
        services.TryAddSingleton<InMemoryResourcePoolStore>();
        services.TryAddSingleton<InMemoryResourceGovernanceStore>();
        services.TryAddSingleton<IWorkflowEventStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowInboxStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowStartIdempotencyStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowOutboxStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IWorkflowProjectionStore>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<ITimerScheduler>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IMessageDispatcher>(provider =>
            provider.GetRequiredService<InMemoryWorkflowProvider>());
        services.TryAddSingleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<InMemoryResourcePoolStore>());
        services.TryAddSingleton<IDurableResourceGovernanceStore>(provider =>
            provider.GetRequiredService<InMemoryResourceGovernanceStore>());
        services.AddSingleton<IDurableProviderRole>(InMemoryDurableProviderRole.Instance);
        return services;
    }

    private sealed record InMemoryDurableProviderRole : IDurableProviderRole
    {
        internal static InMemoryDurableProviderRole Instance { get; } = new();

        public string Name => "in-memory";

        public bool IsDevelopmentOnly => true;
    }
}
