using Microsoft.Extensions.Hosting;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerProviderInitializationHostedService(SqlServerStateDocumentStore store)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => store.InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
