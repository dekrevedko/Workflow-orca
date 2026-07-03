using Microsoft.Extensions.Hosting;

namespace OrcaCore.Hosting.Services;

/// <summary>
/// Hosts timer processing for applications that opt in.
/// </summary>
public sealed class OrcaCoreTimerHostedService : IHostedService
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
