using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;

namespace OrcaCore.Dag.Hosting;

/// <summary>Configures the durable DAG coordinator role.</summary>
public sealed class DagHostOptions
{
    /// <summary>Gets the number of started nonterminal child nodes admitted concurrently.</summary>
    public int MaxConcurrentNodes { get; init; }
}

/// <summary>Registers the DAG coordinator on an existing durable-engine host.</summary>
public static class OrcaCoreDagHostingServiceCollectionExtensions
{
    /// <summary>Adds the DAG coordinator and registry for one immutable host profile.</summary>
    public static IServiceCollection AddOrcaCoreDag(
        this IServiceCollection services,
        DagHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxConcurrentNodes, 1);

        var hasDurableEngine = services
            .Where(descriptor => descriptor.ServiceType == typeof(DurableEngineRoleRegistration))
            .Select(descriptor => descriptor.ImplementationInstance as DurableEngineRoleRegistration)
            .Any(marker => marker?.Role == "durable-engine");
        if (!hasDurableEngine)
        {
            throw new InvalidOperationException(
                "AddOrcaCoreDag requires AddOrcaCoreDurableEngine on the same service collection.");
        }

        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(DagHostRegistration))
            .Select(descriptor => descriptor.ImplementationInstance as DagHostRegistration)
            .SingleOrDefault(marker => marker is not null);
        if (existing is not null)
        {
            if (existing.MaxConcurrentNodes == options.MaxConcurrentNodes)
            {
                return services;
            }

            throw new InvalidOperationException(
                "The DAG host role is already registered with different options.");
        }

        services.AddSingleton(new DagHostRegistration(options.MaxConcurrentNodes));
        services.AddSingleton<DagDefinitionRegistry>();
        services.AddSingleton(provider => new DagCoordinator(
            provider.GetRequiredService<DagDefinitionRegistry>(),
            provider.GetRequiredService<DagHostRegistration>().MaxConcurrentNodes));
        return services;
    }

    private sealed record DagHostRegistration(int MaxConcurrentNodes);
}
