using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Hosting;

/// <summary>Registers the explicit ephemeral engine role.</summary>
public static class OrcaCoreEphemeralEngineServiceCollectionExtensions
{
    public static OrcaCoreEphemeralEngineBuilder AddOrcaCoreEphemeralEngine(
        this IServiceCollection services,
        EphemeralEngineHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var copied = EphemeralWorkflowEngineOptions.FromHostOptions(options);
        global::OrcaCore.Core.Authoring.TypedAuthoringBootstrap.Initialize();
        if (EngineRoleRegistration.RequireAvailableOrSame(
                services,
                "ephemeral-engine",
                copied))
        {
            return new OrcaCoreEphemeralEngineBuilder(services);
        }

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IEphemeralWorkflowHostLifetime)))
            throw new ArgumentException("The ephemeral host lifetime is reserved for the engine role.", nameof(services));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new EphemeralWorkflowEngine(
            provider.GetRequiredService<TimeProvider>(),
            copied,
            provider));
        services.AddSingleton<IEphemeralWorkflowHostLifetime>(provider => provider.GetRequiredService<EphemeralWorkflowEngine>().HostLifetime);
        services.TryAddSingleton(provider =>
        {
            var registry = new EphemeralWorkflowDefinitionRegistry(
                provider.GetRequiredService<EphemeralWorkflowEngine>());
            registry.InstallStagedBatch(
                provider.GetServices<IEphemeralStagedWorkflowDefinition>());
            return registry;
        });
        services.TryAddSingleton<IWorkflowDefinitionRegistry>(provider =>
            provider.GetRequiredService<EphemeralWorkflowDefinitionRegistry>());
        services.TryAddSingleton<EphemeralWorkflowEventRouter>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreEphemeralWorkflowCatalogReadinessHostedService>());
        services.AddSingleton(new EngineRoleRegistration("ephemeral-engine", copied));
        return new OrcaCoreEphemeralEngineBuilder(services);
    }

}

/// <summary>Stages immutable ephemeral definitions for one engine host.</summary>
public sealed class OrcaCoreEphemeralEngineBuilder
{
    private readonly IServiceCollection services;

    internal OrcaCoreEphemeralEngineBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public OrcaCoreEphemeralEngineBuilder AddWorkflow<TInput>(
        EphemeralWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        services.AddSingleton<IEphemeralStagedWorkflowDefinition>(
            new EphemeralStagedWorkflowDefinition<TInput>(definition));
        return this;
    }

    public OrcaCoreEphemeralEngineBuilder AddWorkflow<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        services.AddSingleton<IEphemeralStagedWorkflowDefinition>(
            new EphemeralStagedWorkflowDefinition<TInput, TOutput>(definition));
        return this;
    }
}

internal sealed class OrcaCoreEphemeralWorkflowCatalogReadinessHostedService : IHostedService
{
    private readonly EphemeralHostLifetime lifetime;
    public OrcaCoreEphemeralWorkflowCatalogReadinessHostedService(
        IWorkflowDefinitionRegistry registry, IEphemeralWorkflowHostLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(registry);
        this.lifetime = (EphemeralHostLifetime)lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (await lifetime.StopHostedAsync(cancellationToken).ConfigureAwait(false) is EphemeralHostShutdownResult.Incomplete incomplete)
            throw new EphemeralHostShutdownIncompleteException(incomplete);
    }
}

internal sealed record EngineRoleRegistration(string Role, EphemeralWorkflowEngineOptions Options)
{
    internal static bool RequireAvailableOrSame(
        IServiceCollection services,
        string requestedRole,
        EphemeralWorkflowEngineOptions options)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType.FullName == "OrcaCore.Hosting.DurableEngineRoleRegistration"))
        {
            throw new InvalidOperationException(
                $"OrcaCore durable role is already registered; '{requestedRole}' cannot be combined with it.");
        }

        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(EngineRoleRegistration))
            .Select(descriptor => descriptor.ImplementationInstance as EngineRoleRegistration)
            .FirstOrDefault(marker => marker is not null);
        if (existing is null)
        {
            return false;
        }

        if (string.Equals(existing.Role, requestedRole, StringComparison.Ordinal) &&
            SameProfile(existing.Options, options))
        {
            return true;
        }

        throw new InvalidOperationException(
            $"OrcaCore engine role '{existing.Role}' is already registered; '{requestedRole}' cannot be combined with it.");
    }

    private static bool SameProfile(EphemeralWorkflowEngineOptions left, EphemeralWorkflowEngineOptions right) =>
        left.MaxConcurrentExecutionPathsPerInstance == right.MaxConcurrentExecutionPathsPerInstance &&
        left.TerminalRetention == right.TerminalRetention &&
        left.StepThrottles.Count == right.StepThrottles.Count &&
        left.StepThrottles.All(pair => right.StepThrottles.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
        left.TransientPools.Count == right.TransientPools.Count &&
        left.TransientPools.All(pair => right.TransientPools.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
