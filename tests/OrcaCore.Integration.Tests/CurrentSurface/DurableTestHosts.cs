using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Provider.Abstractions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.PostgreSql;

namespace OrcaCore.Integration.Tests.CurrentSurface;

internal static class DurableTestHosts
{
    internal static SharedInMemoryDurableStores CreateSharedInMemoryStores()
    {
        return new SharedInMemoryDurableStores(
            new InMemoryWorkflowProvider(),
            new InMemoryResourcePoolStore(),
            new InMemoryResourceGovernanceStore());
    }

    internal static ServiceProvider BuildInMemory(SharedInMemoryDurableStores stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        var services = new ServiceCollection();
        services.AddSingleton(stores.Workflow);
        services.AddSingleton<IWorkflowEventStore>(stores.Workflow);
        services.AddSingleton<IWorkflowInboxStore>(stores.Workflow);
        services.AddSingleton<IWorkflowStartIdempotencyStore>(stores.Workflow);
        services.AddSingleton<IWorkflowOutboxStore>(stores.Workflow);
        services.AddSingleton<IWorkflowProjectionStore>(stores.Workflow);
        services.AddSingleton<IWorkflowRetentionStore>(stores.Workflow);
        services.AddSingleton<ITimerScheduler>(stores.Workflow);
        services.AddSingleton<IMessageDispatcher>(stores.Workflow);
        services.AddSingleton(stores.ResourcePool);
        services.AddSingleton<IResourcePoolStore>(stores.ResourcePool);
        services.AddSingleton(stores.ResourceGovernance);
        services.AddSingleton<IDurableResourceGovernanceStore>(stores.ResourceGovernance);
        services.AddSingleton<IDurableProviderRole>(SharedInMemoryDurableProviderRole.Instance);
        services.AddOrcaCoreDurableEngine(CreateOptions());
        return services.BuildServiceProvider();
    }

    internal static ServiceProvider BuildPostgreSql(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var services = new ServiceCollection();
        services.AddOrcaCorePostgreSqlDurableProvider(
            new PostgreSqlDurableProviderOptions(connectionString, "public"));
        services.AddOrcaCoreDurableEngine(CreateOptions());
        return services.BuildServiceProvider();
    }

    internal static DurableEngineHostOptions CreateOptions()
    {
        return new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create("integration"),
                Pools = []
            }
        };
    }

    internal sealed record SharedInMemoryDurableStores(
        InMemoryWorkflowProvider Workflow,
        InMemoryResourcePoolStore ResourcePool,
        InMemoryResourceGovernanceStore ResourceGovernance);

    private sealed record SharedInMemoryDurableProviderRole : IDurableProviderRole
    {
        internal static SharedInMemoryDurableProviderRole Instance { get; } = new();

        public string Name => "shared-in-memory-integration";

        public bool IsDevelopmentOnly => true;
    }
}
