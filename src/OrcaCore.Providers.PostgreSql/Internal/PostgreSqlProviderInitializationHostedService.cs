using Microsoft.Extensions.Hosting;

namespace OrcaCore.Providers.PostgreSql;

internal sealed class PostgreSqlProviderInitializationHostedService(
    PostgreSqlWorkflowStore workflowStore,
    PostgreSqlResourcePoolStore resourcePoolStore,
    PostgreSqlResourceGovernanceStore resourceGovernanceStore) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await workflowStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await resourcePoolStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await resourceGovernanceStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
