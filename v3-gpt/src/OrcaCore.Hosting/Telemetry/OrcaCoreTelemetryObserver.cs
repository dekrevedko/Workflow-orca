using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Hosting.Telemetry;

internal sealed class OrcaCoreTelemetryObserver(
    ILogger<OrcaCoreTelemetryObserver> logger,
    OrcaCoreTelemetryInstruments instruments) :
    IWorkflowRuntimeObserver,
    IOutboxPumpObserver,
    IDurableDriverObserver
{
    public ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        instruments.RecordCommandProcessed(
            observation.CommandType,
            observation.Outcome.ToString(),
            observation.DefinitionId,
            observation.DefinitionVersion,
            observation.Status,
            observation.Duration);
        instruments.RecordRuntimeSignals(observation);

        using var scope = logger.BeginScope(CreateCommandScope(observation));
        OrcaCoreTelemetryLog.CommandCompleted(
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

        var result = ToResultTag(observation.Result);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.OutboxKindKey] = observation.Kind,
            [OrcaCoreDiagnostics.OutboxRecordIdKey] = observation.OutboxRecordId.ToString(),
            [OrcaCoreDiagnostics.OutboxResultKey] = result
        });
        instruments.RecordOutboxDispatch(
            observation.Kind,
            result,
            observation.Duration);

        if (observation.Result is DispatchResult.PermanentFailure)
        {
            OrcaCoreTelemetryLog.OutboxRecordPermanentFailure(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                observation.Duration.TotalMilliseconds);
        }

        if (observation.Exception is not null)
        {
            OrcaCoreTelemetryLog.OutboxRecordDispatchException(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                observation.Exception.GetType().Name,
                observation.Exception.Message);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnPumpCompletedAsync(
        OutboxPumpObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        OrcaCoreTelemetryLog.OutboxPumpCompleted(
            logger,
            observation.ClaimedCount,
            observation.DispatchAttemptCount,
            observation.SuccessCount,
            observation.RetryableFailureCount,
            observation.PermanentFailureCount);
        if (observation.PermanentFailureCount > 0)
        {
            OrcaCoreTelemetryLog.OutboxPermanentFailures(
                logger,
                observation.PermanentFailureCount,
                observation.DispatchAttemptCount);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnSegmentCompletedAsync(
        DurableDriverSegmentObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        instruments.RecordDriverSegment(observation);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnContinuationStartedAsync(
        DurableContinuationObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        instruments.RecordContinuation(observation);
        return ValueTask.CompletedTask;
    }

    private static IReadOnlyDictionary<string, object?> CreateCommandScope(
        WorkflowRuntimeObservation observation)
    {
        var scope = new Dictionary<string, object?>
        {
            ["orca.instance.id"] = observation.InstanceId.ToString(),
            ["orca.command.type"] = observation.CommandType,
            ["orca.command.outcome"] = observation.Outcome.ToString()
        };

        if (observation.DefinitionId is not null)
        {
            scope["orca.definition.id"] = observation.DefinitionId.Value.ToString();
        }

        if (observation.DefinitionVersion is not null)
        {
            scope["orca.definition.version"] = observation.DefinitionVersion.Value.ToString();
        }

        if (observation.Status is not null)
        {
            scope["orca.status"] = observation.Status.Value.ToString();
        }

        if (Activity.Current is { } activity)
        {
            scope["trace_id"] = activity.TraceId.ToString();
            scope["span_id"] = activity.SpanId.ToString();
        }

        return scope;
    }

    private static string ToResultTag(DispatchResult result)
    {
        return result switch
        {
            DispatchResult.Success => "success",
            DispatchResult.RetryableFailure => "retryable",
            DispatchResult.PermanentFailure => "permanent",
            _ => throw new UnreachableException()
        };
    }
}

internal static partial class OrcaCoreTelemetryLog
{
    [LoggerMessage(
        EventId = 1001,
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
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Outbox pump completed with {ClaimedCount} claimed, {DispatchAttemptCount} attempted, {SuccessCount} succeeded, {RetryableFailureCount} retryable, and {PermanentFailureCount} permanent.")]
    internal static partial void OutboxPumpCompleted(
        ILogger logger,
        int claimedCount,
        int dispatchAttemptCount,
        int successCount,
        int retryableFailureCount,
        int permanentFailureCount);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Error,
        Message = "Outbox pump recorded {PermanentFailureCount} permanent failure(s) across {DispatchAttemptCount} dispatch attempt(s).")]
    internal static partial void OutboxPermanentFailures(
        ILogger logger,
        int permanentFailureCount,
        int dispatchAttemptCount);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Error,
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} was marked as a permanent failure after {DurationMs} ms.")]
    internal static partial void OutboxRecordPermanentFailure(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        double durationMs);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Error,
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} threw {ExceptionType}: {ExceptionMessage}")]
    internal static partial void OutboxRecordDispatchException(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        string exceptionType,
        string exceptionMessage);
}
