using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        var provider = services.BuildServiceProvider();
        return new SharedInMemoryDurableStores(
            provider,
            provider.GetRequiredService<IWorkflowEventStore>(),
            provider.GetRequiredService<IWorkflowInboxStore>(),
            provider.GetRequiredService<IWorkflowStartIdempotencyStore>(),
            provider.GetRequiredService<IWorkflowOutboxStore>(),
            provider.GetRequiredService<IWorkflowProjectionStore>(),
            provider.GetRequiredService<ITimerScheduler>(),
            provider.GetRequiredService<IMessageDispatcher>(),
            provider.GetRequiredService<IResourcePoolStore>(),
            provider.GetRequiredService<IDurableResourceGovernanceStore>());
    }

    internal static ServiceProvider BuildInMemory(SharedInMemoryDurableStores stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        var services = new ServiceCollection();
        services.AddSingleton(stores.EventStore);
        services.AddSingleton(stores.InboxStore);
        services.AddSingleton(stores.StartIdempotencyStore);
        services.AddSingleton(stores.OutboxStore);
        services.AddSingleton(stores.ProjectionStore);
        services.AddSingleton(stores.TimerScheduler);
        services.AddSingleton(stores.MessageDispatcher);
        services.AddSingleton(stores.ResourcePoolStore);
        services.AddSingleton(stores.ResourceGovernanceStore);
        services.AddSingleton<IDurableProviderRole>(SharedInMemoryDurableProviderRole.Instance);
        services.AddOrcaCoreDurableEngine(CreateOptions());
        return services.BuildServiceProvider();
    }

    internal static async Task<RunningDurableHost> StartPostgreSqlAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOrcaCorePostgreSqlDurableProvider(
            new PostgreSqlDurableProviderOptions(connectionString, "public"));
        builder.Services.AddOrcaCoreDurableEngine(CreateOptions());
        var host = builder.Build();
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);
            return new RunningDurableHost(host);
        }
        catch
        {
            host.Dispose();
            throw;
        }
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
        ServiceProvider Owner,
        IWorkflowEventStore EventStore,
        IWorkflowInboxStore InboxStore,
        IWorkflowStartIdempotencyStore StartIdempotencyStore,
        IWorkflowOutboxStore OutboxStore,
        IWorkflowProjectionStore ProjectionStore,
        ITimerScheduler TimerScheduler,
        IMessageDispatcher MessageDispatcher,
        IResourcePoolStore ResourcePoolStore,
        IDurableResourceGovernanceStore ResourceGovernanceStore) : IDisposable
    {
        public void Dispose() => Owner.Dispose();
    }

    internal sealed class RunningDurableHost(IHost host) : IAsyncDisposable
    {
        internal IServiceProvider Services => host.Services;

        public async ValueTask DisposeAsync()
        {
            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
            host.Dispose();
        }
    }

    private sealed record SharedInMemoryDurableProviderRole : IDurableProviderRole
    {
        internal static SharedInMemoryDurableProviderRole Instance { get; } = new();

        public string Name => "shared-in-memory-integration";

        public bool IsDevelopmentOnly => true;
    }
}
