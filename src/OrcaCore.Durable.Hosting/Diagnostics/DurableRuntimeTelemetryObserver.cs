using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Hosting.Diagnostics;

internal sealed class CompositeWorkflowRuntimeObserver(
    DurableFacadeNotificationHub notifications,
    DurableRuntimeTelemetryObserver telemetry) : IWorkflowRuntimeObserver
{
    public async ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken)
    {
        await notifications.OnCommandCompletedAsync(observation, cancellationToken).ConfigureAwait(false);
        await telemetry.OnCommandCompletedAsync(observation, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class DurableRuntimeTelemetryObserver(
    ILogger<DurableRuntimeTelemetryObserver> logger) :
    IWorkflowRuntimeObserver,
    IOutboxPumpObserver,
    IDurableDriverObserver
{
    public ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OrcaCoreDurableDiagnostics.RecordCommand(observation);
        using var scope = logger.BeginScope(CommandScope(observation));
        DurableTelemetryLog.CommandCompleted(
            logger,
            observation.CommandType,
            observation.Outcome.ToString(),
            observation.InstanceId.ToString(),
            observation.StreamVersion.Value,
            observation.EventCount,
            observation.Duration.TotalMilliseconds);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnDispatchCompletedAsync(
        OutboxDispatchObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OrcaCoreDurableDiagnostics.RecordOutboxDispatch(observation);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.OutboxKindKey] = observation.Kind,
            [OrcaCoreDiagnostics.OutboxRecordIdKey] = observation.OutboxRecordId.ToString(),
            [OrcaCoreDiagnostics.OutboxResultKey] = observation.Result.ToString()
        });
        if (observation.Result is DispatchResult.PermanentFailure)
        {
            DurableTelemetryLog.OutboxPermanentFailure(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                observation.Duration.TotalMilliseconds);
        }

        if (observation.Exception is { } exception)
        {
            DurableTelemetryLog.OutboxException(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                exception.GetType().Name,
                exception.Message);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnPumpCompletedAsync(
        OutboxPumpObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DurableTelemetryLog.OutboxPumpCompleted(
            logger,
            observation.ClaimedCount,
            observation.DispatchAttemptCount,
            observation.SuccessCount,
            observation.RetryableFailureCount,
            observation.PermanentFailureCount);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnSegmentCompletedAsync(
        DurableDriverSegmentObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OrcaCoreDurableDiagnostics.RecordDriverSegment(observation);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnContinuationStartedAsync(
        DurableContinuationObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OrcaCoreDurableDiagnostics.RecordContinuation(observation);
        return ValueTask.CompletedTask;
    }

    private static IReadOnlyDictionary<string, object?> CommandScope(WorkflowRuntimeObservation observation)
    {
        var scope = new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.InstanceIdKey] = observation.InstanceId.ToString(),
            [OrcaCoreDiagnostics.CommandTypeKey] = observation.CommandType,
            [OrcaCoreDiagnostics.CommandOutcomeKey] = observation.Outcome.ToString()
        };
        if (observation.DefinitionId is { } definitionId)
        {
            scope[OrcaCoreDiagnostics.DefinitionIdKey] = definitionId.ToString();
        }

        if (observation.DefinitionVersion is { } definitionVersion)
        {
            scope[OrcaCoreDiagnostics.DefinitionVersionKey] = definitionVersion.ToString();
        }

        if (observation.Status is { } status)
        {
            scope[OrcaCoreDiagnostics.StatusKey] = status.ToString();
        }

        if (Activity.Current is { } activity)
        {
            scope[OrcaCoreDiagnostics.TraceIdKey] = activity.TraceId.ToString();
            scope[OrcaCoreDiagnostics.SpanIdKey] = activity.SpanId.ToString();
            scope[OrcaCoreDiagnostics.TraceFlagsKey] = activity.ActivityTraceFlags.ToString();
        }

        return scope;
    }
}

internal static partial class DurableTelemetryLog
{
    private const int CommandCompletedEventId = 1001;
    private const int OutboxPumpCompletedEventId = 1101;
    private const int OutboxPermanentFailureEventId = 1102;
    private const int OutboxExceptionEventId = 1103;

    [LoggerMessage(
        EventId = CommandCompletedEventId,
        Level = LogLevel.Information,
        Message = "Durable command {CommandType} completed with {Outcome} for instance {InstanceId} at stream version {StreamVersion} after {DurationMs} ms and {EventCount} event(s).")]
    internal static partial void CommandCompleted(
        ILogger logger,
        string commandType,
        string outcome,
        string instanceId,
        long streamVersion,
        int eventCount,
        double durationMs);

    [LoggerMessage(
        EventId = OutboxPumpCompletedEventId,
        Level = LogLevel.Information,
        Message = "Outbox pump completed with {ClaimedCount} claimed, {AttemptedCount} attempted, {SuccessCount} succeeded, {RetryableCount} retryable, and {PermanentCount} permanent.")]
    internal static partial void OutboxPumpCompleted(
        ILogger logger,
        int claimedCount,
        int attemptedCount,
        int successCount,
        int retryableCount,
        int permanentCount);

    [LoggerMessage(
        EventId = OutboxPermanentFailureEventId,
        Level = LogLevel.Error,
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} was marked as a permanent failure after {DurationMs} ms.")]
    internal static partial void OutboxPermanentFailure(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        double durationMs);

    [LoggerMessage(
        EventId = OutboxExceptionEventId,
        Level = LogLevel.Error,
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} threw {ExceptionType}: {ExceptionMessage}")]
    internal static partial void OutboxException(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        string exceptionType,
        string exceptionMessage);
}
