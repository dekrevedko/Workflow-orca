using Microsoft.Extensions.Hosting;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts durable outbox pumping for applications that opt in.
/// </summary>
public sealed class OrcaCoreOutboxPumpHostedService : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
