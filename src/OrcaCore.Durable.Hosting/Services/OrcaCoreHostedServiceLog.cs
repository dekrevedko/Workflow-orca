using Microsoft.Extensions.Logging;

namespace OrcaCore.Hosting.Services;

internal static partial class OrcaCoreHostedServiceLog
{
    private const int ProcessingCycleFailedEventId = 1901;

    [LoggerMessage(
        EventId = ProcessingCycleFailedEventId,
        Level = LogLevel.Warning,
        Message = "OrcaCore hosted service {ServiceName} failed a processing cycle. Retrying after {FailureBackoff}.")]
    internal static partial void ProcessingCycleFailed(
        ILogger logger,
        string serviceName,
        TimeSpan failureBackoff,
        Exception exception);
}
