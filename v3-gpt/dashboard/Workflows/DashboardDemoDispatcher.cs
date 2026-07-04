using System.Text;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Dashboard.Workflows;

public sealed class DashboardDemoDispatcher(
    ILogger<DashboardDemoDispatcher> logger) : IMessageDispatcher
{
    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        var body = Encoding.UTF8.GetString(record.Payload);
        if (record.Kind == "external-job-start" &&
            body.Contains("permanent-dispatch", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(
                "Dashboard demo dispatcher forced permanent failure for {Kind} record {OutboxRecordId}.",
                record.Kind,
                record.OutboxRecordId);
            return Task.FromResult(DispatchResult.PermanentFailure);
        }

        if (record.Kind == "external-job-start" &&
            body.Contains("retryable-dispatch", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Dashboard demo dispatcher forced retryable failure for {Kind} record {OutboxRecordId}.",
                record.Kind,
                record.OutboxRecordId);
            return Task.FromResult(DispatchResult.RetryableFailure);
        }

        logger.LogInformation(
            "Dashboard demo dispatcher accepted {Kind} record {OutboxRecordId}.",
            record.Kind,
            record.OutboxRecordId);
        return Task.FromResult(DispatchResult.Success);
    }
}
