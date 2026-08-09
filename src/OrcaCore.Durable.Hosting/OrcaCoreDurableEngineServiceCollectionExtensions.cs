using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;

namespace OrcaCore.Hosting;

/// <summary>Registers the durable engine and callback-only ingress roles.</summary>
public static class OrcaCoreDurableEngineServiceCollectionExtensions
{
    /// <summary>Registers one durable engine role using one complete provider role set.</summary>
    public static OrcaCoreDurableEngineBuilder AddOrcaCoreDurableEngine(
        this IServiceCollection services,
        DurableEngineHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var copied = options.ValidateAndCopy();
        RequireProviderRole(services);
        var fingerprint = ProfileFingerprint(copied);
        if (RequireAvailableOrSame(services, "durable-engine", fingerprint))
        {
            return new OrcaCoreDurableEngineBuilder(services);
        }

        AddCommonServices(services);
        services.TryAddSingleton(provider =>
            new SerializedResourceGovernanceAggregate(
                provider.GetRequiredService<IDurableResourceGovernanceStore>(),
                new DurableResourcePoolOptions
                {
                    PartitionId = copied.PartitionId,
                    Pools = copied.ResourcePools.Values
                        .OrderBy(pool => pool.Name.Value, StringComparer.Ordinal)
                        .ToArray()
                }));
        services.Replace(ServiceDescriptor.Singleton<IResourcePoolStore>(provider =>
            provider.GetRequiredService<SerializedResourceGovernanceAggregate>()));
        services.AddOptions<DurableHostedServiceOptions>();
        services.TryAddSingleton(provider =>
            new DurableDefinitionRegistry(copied.StructuredExecution, provider));
        AddDurableCommandServices(services);
        services.TryAddSingleton(provider => new DurableWorkflowRuntime(
            provider.GetRequiredService<DurableCommandProcessor>(),
            provider.GetRequiredService<DurableDefinitionRegistry>(),
            provider.GetRequiredService<TimeProvider>(),
            DurableDriverBudget.Default,
            provider.GetRequiredService<IWorkflowProjectionStore>(),
            provider.GetService<IDurableDriverObserver>()));
        services.TryAddSingleton(provider =>
        {
            var registry = new DurableWorkflowDefinitionRegistry(
                provider.GetRequiredService<DurableWorkflowRuntime>(),
                provider.GetRequiredService<IWorkflowProjectionStore>(),
                provider.GetRequiredService<IWorkflowEventStore>(),
                provider.GetRequiredService<DurableCommandProcessor>(),
                provider.GetRequiredService<DurableFacadeNotificationHub>(),
                provider.GetRequiredService<TimeProvider>(),
                copied.ResourcePools.Values.Select(pool => pool.Name),
                provider.GetService<IWorkflowEventDispatcher>() is not null);
            registry.InstallStagedBatch(
                provider.GetServices<IDurableStagedWorkflowDefinition>());
            return registry;
        });
        services.TryAddSingleton<IWorkflowDefinitionRegistry>(provider =>
            provider.GetRequiredService<DurableWorkflowDefinitionRegistry>());
        services.TryAddSingleton<IWorkflowEventIngress>(provider =>
            new DurableWorkflowEventIngress(
                provider.GetRequiredService<DurableWorkflowRuntime>(),
                provider.GetRequiredService<IWorkflowProjectionStore>(),
                provider.GetRequiredService<IWorkflowInboxStore>()));
        services.TryAddSingleton(provider => new DurableInboxContinuationPump(
            provider.GetRequiredService<IWorkflowInboxStore>(),
            provider.GetRequiredService<IWorkflowProjectionStore>(),
            provider.GetRequiredService<DurableWorkflowRuntime>()));
        services.TryAddSingleton(provider => new DurableContinuationPump(
            provider.GetRequiredService<IWorkflowOutboxStore>(),
            provider.GetRequiredService<DurableWorkflowRuntime>(),
            provider.GetRequiredService<DurableCommandProcessor>(),
            provider.GetRequiredService<TimeProvider>(),
            observer: provider.GetService<IDurableDriverObserver>(),
            inboxStore: provider.GetRequiredService<IWorkflowInboxStore>()));
        services.TryAddSingleton<IDurableResourcePoolManagement>(provider =>
            new DurableResourcePoolManagement(
                provider.GetRequiredService<SerializedResourceGovernanceAggregate>()));
        services.TryAddSingleton(provider =>
            new DurableResourceLeaseRecovery(
                provider.GetRequiredService<DurableCommandProcessor>(),
                provider.GetRequiredService<IWorkflowProjectionStore>(),
                provider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton(provider =>
            new DurableResourceLeaseDiagnostics(
                provider.GetRequiredService<DurableCommandProcessor>(),
                provider.GetRequiredService<IWorkflowProjectionStore>()));
        services.TryAddSingleton<IDurableResourceLeaseRecovery, HostedDurableResourceLeaseRecovery>();
        services.TryAddSingleton<IDurableResourceLeaseDiagnostics, HostedDurableResourceLeaseDiagnostics>();

        AddProgressionLoops(services);
        services.AddSingleton(new DurableEngineRoleRegistration("durable-engine", fingerprint));
        return new OrcaCoreDurableEngineBuilder(services);
    }

    /// <summary>
    /// Registers definition-less durable event ingress using one complete provider role set.
    /// </summary>
    public static IServiceCollection AddOrcaCoreDurableEventIngress(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RequireProviderRole(services);
        if (RequireAvailableOrSame(services, "durable-event-ingress", "v1"))
        {
            return services;
        }

        AddCommonServices(services);
        AddDurableCommandServices(services);
        services.TryAddSingleton<IWorkflowEventIngress>(provider =>
        {
            var definitions = new DurableDefinitionRegistry();
            var runtime = new DurableWorkflowRuntime(
                provider.GetRequiredService<DurableCommandProcessor>(),
                definitions,
                provider.GetRequiredService<TimeProvider>(),
                DurableDriverBudget.Default,
                provider.GetRequiredService<IWorkflowProjectionStore>());
            return new DurableWorkflowEventIngress(
                runtime,
                provider.GetRequiredService<IWorkflowProjectionStore>(),
                provider.GetRequiredService<IWorkflowInboxStore>(),
                driveAfterAcceptance: false);
        });
        services.AddSingleton(new DurableEngineRoleRegistration("durable-event-ingress", "v1"));
        return services;
    }

    private static void AddCommonServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ILoggerFactory>(_ => NullLoggerFactory.Instance);
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
    }

    private static void AddDurableCommandServices(IServiceCollection services)
    {
        services.TryAddSingleton<DurableFacadeNotificationHub>();
        services.TryAddSingleton<IWorkflowRuntimeObserver>(provider =>
            provider.GetRequiredService<DurableFacadeNotificationHub>());
        services.TryAddSingleton(provider => new DurableCommandRuntime(
            provider.GetRequiredService<IWorkflowEventStore>(),
            provider.GetService<IResourcePoolStore>()));
        services.TryAddSingleton(provider => new DurableCommandProcessor(
            provider.GetRequiredService<DurableCommandRuntime>(),
            provider.GetRequiredService<IWorkflowRuntimeObserver>()));
    }

    private static void AddProgressionLoops(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreDurableWorkflowCatalogReadinessHostedService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreContinuationPumpHostedService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreTimerHostedService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreOperationalSweepHostedService>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, OrcaCoreWorkflowEventOutboxPumpHostedService>());

        if (services.Any(descriptor => descriptor.ServiceType == typeof(IMessageDispatcher)))
        {
            services.TryAddSingleton(provider => new DurableOutboxPump(
                provider.GetRequiredService<IWorkflowOutboxStore>(),
                provider.GetRequiredService<IMessageDispatcher>(),
                provider.GetService<IOutboxPumpObserver>(),
                provider.GetRequiredService<TimeProvider>()));
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IHostedService, OrcaCoreOutboxPumpHostedService>());
        }
    }

    private static IDurableProviderRole RequireProviderRole(IServiceCollection services)
    {
        var roles = services
            .Where(descriptor => descriptor.ServiceType == typeof(IDurableProviderRole))
            .Select(descriptor => descriptor.ImplementationInstance as IDurableProviderRole)
            .Where(role => role is not null)
            .Cast<IDurableProviderRole>()
            .ToArray();
        return roles.Length switch
        {
            1 => roles[0],
            0 => throw new InvalidOperationException(
                "A complete durable provider role must be registered before the durable host role."),
            _ => throw new InvalidOperationException(
                "Exactly one complete durable provider role may be registered.")
        };
    }

    private static bool RequireAvailableOrSame(
        IServiceCollection services,
        string requestedRole,
        string optionsFingerprint)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType.FullName == "OrcaCore.Hosting.EngineRoleRegistration"))
        {
            throw new InvalidOperationException(
                $"OrcaCore engine role 'ephemeral-engine' is already registered; '{requestedRole}' cannot be combined with it.");
        }

        var existing = services
            .Where(descriptor => descriptor.ServiceType == typeof(DurableEngineRoleRegistration))
            .Select(descriptor => descriptor.ImplementationInstance as DurableEngineRoleRegistration)
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
            $"OrcaCore durable role '{existing.Role}' is already registered; '{requestedRole}' cannot be combined with it.");
    }

    private static string ProfileFingerprint(ValidatedDurableEngineHostOptions options)
    {
        var throttles = options.StructuredExecution.StepThrottles
            .OrderBy(throttle => throttle.StepType.AssemblyQualifiedName, StringComparer.Ordinal)
            .Select(throttle =>
                $"{throttle.StepType.AssemblyQualifiedName}={throttle.MaxConcurrency}");
        var pools = options.ResourcePools.Values
            .OrderBy(pool => pool.Name.Value, StringComparer.Ordinal)
            .Select(pool =>
                $"{pool.Name.Value}={pool.Capacity}:{pool.ReviewAfter.Ticks}");
        return string.Join(
            "|",
            options.StructuredExecution.MaxConcurrentExecutionPathsPerInstance,
            string.Join(",", throttles),
            options.PartitionId.Value,
            string.Join(",", pools));
    }
}

/// <summary>Stages immutable durable definitions for one engine host.</summary>
public sealed class OrcaCoreDurableEngineBuilder
{
    private readonly IServiceCollection services;

    internal OrcaCoreDurableEngineBuilder(IServiceCollection services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public OrcaCoreDurableEngineBuilder AddWorkflow<TInput>(
        DurableWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        services.AddSingleton<IDurableStagedWorkflowDefinition>(
            new DurableStagedWorkflowDefinition<TInput>(definition));
        return this;
    }

    public OrcaCoreDurableEngineBuilder AddWorkflow<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        services.AddSingleton<IDurableStagedWorkflowDefinition>(
            new DurableStagedWorkflowDefinition<TInput, TOutput>(definition));
        return this;
    }
}

internal sealed class OrcaCoreDurableWorkflowCatalogReadinessHostedService : IHostedService
{
    public OrcaCoreDurableWorkflowCatalogReadinessHostedService(
        IWorkflowDefinitionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed record DurableEngineRoleRegistration(
    string Role,
    string OptionsFingerprint);
