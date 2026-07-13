using Microsoft.Extensions.Logging;

namespace OrcaCore.Hosting.Services;

internal static partial class OrcaCoreHostedServiceLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "OrcaCore hosted service {ServiceName} failed a processing cycle. Retrying after {FailureBackoff}.")]
    internal static partial void ProcessingCycleFailed(
        ILogger logger,
        string serviceName,
        TimeSpan failureBackoff,
        Exception exception);
}
