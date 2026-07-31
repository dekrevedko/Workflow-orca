using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Hosting;

/// <summary>Registers the explicit ephemeral engine role.</summary>
public static class OrcaCoreEphemeralEngineServiceCollectionExtensions
{
    public static IServiceCollection AddOrcaCoreEphemeralEngine(
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
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(provider => new EphemeralWorkflowEngine(
            provider.GetRequiredService<TimeProvider>(),
            copied,
            provider));
        services.TryAddSingleton<EphemeralWorkflowDefinitionRegistry>();
        services.TryAddSingleton<IWorkflowDefinitionRegistry>(provider =>
            provider.GetRequiredService<EphemeralWorkflowDefinitionRegistry>());
        services.TryAddSingleton<IWorkflowEventClient, EphemeralWorkflowEventClient>();
        services.AddSingleton(new EngineRoleRegistration("ephemeral-engine", fingerprint));
        return services;
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
