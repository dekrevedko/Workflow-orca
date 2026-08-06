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
        var fingerprint = ProfileFingerprint(copied);
        if (EngineRoleRegistration.RequireAvailableOrSame(
                services,
                "ephemeral-engine",
                fingerprint))
        {
            return new OrcaCoreEphemeralEngineBuilder(services);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new EphemeralWorkflowEngine(
            provider.GetRequiredService<TimeProvider>(),
            copied,
            provider));
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
        services.TryAddSingleton<IWorkflowEventClient, EphemeralWorkflowEventClient>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreEphemeralWorkflowCatalogReadinessHostedService>());
        services.AddSingleton(new EngineRoleRegistration("ephemeral-engine", fingerprint));
        return new OrcaCoreEphemeralEngineBuilder(services);
    }

    private static string ProfileFingerprint(EphemeralWorkflowEngineOptions options)
    {
        var throttles = options.StepThrottles
            .OrderBy(pair => pair.Key.AssemblyQualifiedName, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key.AssemblyQualifiedName}={pair.Value}");
        var pools = options.TransientPools
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}");
        return string.Join(
            "|",
            options.MaxConcurrentExecutionPathsPerInstance,
            string.Join(",", throttles),
            string.Join(",", pools));
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
    public OrcaCoreEphemeralWorkflowCatalogReadinessHostedService(
        IWorkflowDefinitionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed record EngineRoleRegistration(string Role, string OptionsFingerprint)
{
    internal static bool RequireAvailableOrSame(
        IServiceCollection services,
        string requestedRole,
        string optionsFingerprint)
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
            string.Equals(existing.OptionsFingerprint, optionsFingerprint, StringComparison.Ordinal))
        {
            return true;
        }

        throw new InvalidOperationException(
            $"OrcaCore engine role '{existing.Role}' is already registered; '{requestedRole}' cannot be combined with it.");
    }
}
