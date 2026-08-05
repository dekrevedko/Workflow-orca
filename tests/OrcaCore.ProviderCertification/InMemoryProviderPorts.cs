using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.ProviderCertification;

internal sealed class InMemoryProviderPorts : IDisposable
{
    private readonly ServiceProvider services;

    private InMemoryProviderPorts(ServiceProvider services)
    {
        this.services = services;
    }

    internal IWorkflowEventStore EventStore => services.GetRequiredService<IWorkflowEventStore>();

    internal IWorkflowInboxStore InboxStore => services.GetRequiredService<IWorkflowInboxStore>();

    internal IWorkflowStartIdempotencyStore StartIdempotencyStore =>
        services.GetRequiredService<IWorkflowStartIdempotencyStore>();

    internal IWorkflowOutboxStore OutboxStore => services.GetRequiredService<IWorkflowOutboxStore>();

    internal IWorkflowProjectionStore ProjectionStore => services.GetRequiredService<IWorkflowProjectionStore>();

    internal ITimerScheduler TimerScheduler => services.GetRequiredService<ITimerScheduler>();

    internal IResourcePoolStore ResourcePoolStore => services.GetRequiredService<IResourcePoolStore>();

    internal IDurableResourceGovernanceStore ResourceGovernanceStore =>
        services.GetRequiredService<IDurableResourceGovernanceStore>();

    internal static InMemoryProviderPorts Create()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        return new InMemoryProviderPorts(services.BuildServiceProvider());
    }

    public void Dispose() => services.Dispose();
}
