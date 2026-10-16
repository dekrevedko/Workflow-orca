using Microsoft.Extensions.Logging;

namespace OrcaCore.Hosting.Services;

internal sealed class HostedServiceFailureBoundary(
    string serviceName,
    ILogger logger,
    TimeProvider timeProvider)
{
    internal async Task RunAsync(
        Func<CancellationToken, Task> operation,
        TimeSpan failureBackoff,
        CancellationToken stoppingToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            await operation(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            OrcaCoreHostedServiceLog.ProcessingCycleFailed(logger, serviceName, failureBackoff, exception);
            await Task.Delay(failureBackoff, timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }
}
