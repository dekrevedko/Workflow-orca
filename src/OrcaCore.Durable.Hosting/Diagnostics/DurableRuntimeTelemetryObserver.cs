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

    public ValueTask OnProviderCommitFailedAsync(
        WorkflowProviderCommitFailureObservation observation,
        CancellationToken cancellationToken) =>
        telemetry.OnProviderCommitFailedAsync(observation, cancellationToken);
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
        LogProviderCommit(observation);
        LogRuntimeEvents(observation);
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
            [OrcaCoreDiagnostics.OutboxAttemptKey] = observation.Attempt,
            [OrcaCoreDiagnostics.OutboxResultKey] = observation.Result.ToString()
        });
        if (observation.Result is DispatchResult.PermanentFailure)
        {
            DurableTelemetryLog.OutboxPermanentFailure(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                observation.Attempt,
                observation.Duration.TotalMilliseconds);
        }

        if (observation.Exception is { } exception)
        {
            DurableTelemetryLog.OutboxException(
                logger,
                observation.Kind,
                observation.OutboxRecordId.ToString(),
                observation.Attempt,
                exception.GetType().Name,
                exception.Message);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnProviderCommitFailedAsync(
        WorkflowProviderCommitFailureObservation observation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.InstanceIdKey] = observation.InstanceId.ToString(),
            [OrcaCoreDiagnostics.CommandTypeKey] = observation.CommandType,
            [OrcaCoreDiagnostics.ProviderNameKey] = observation.ProviderName,
            [OrcaCoreDiagnostics.ProviderOperationKey] = OrcaCoreDiagnostics.AppendProviderOperation,
            [OrcaCoreDiagnostics.ExpectedStreamVersionKey] = observation.ExpectedStreamVersion.Value
        });
        DurableTelemetryLog.ProviderCommitFailed(
            logger,
            observation.ProviderName,
            observation.ExpectedStreamVersion.Value,
            observation.Duration.TotalMilliseconds,
            observation.Exception.GetType().Name,
            observation.Exception.Message,
            observation.Exception);
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

    private void LogProviderCommit(WorkflowRuntimeObservation observation)
    {
        if (!observation.ProviderCommitAttempted)
        {
            return;
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            [OrcaCoreDiagnostics.ProviderNameKey] = observation.ProviderName,
            [OrcaCoreDiagnostics.ProviderOperationKey] = observation.ProviderOperation,
            [OrcaCoreDiagnostics.ExpectedStreamVersionKey] = observation.ExpectedStreamVersion.Value,
            [OrcaCoreDiagnostics.StreamVersionKey] = observation.StreamVersion.Value
        });
        if (observation.Outcome is DurableCommandOutcome.Conflict)
        {
            DurableTelemetryLog.ProviderCommitConflict(
                logger,
                observation.ProviderName,
                observation.ExpectedStreamVersion.Value,
                observation.StreamVersion.Value,
                observation.ProviderCommitDuration.TotalMilliseconds);
        }
        else
        {
            DurableTelemetryLog.ProviderCommitCompleted(
                logger,
                observation.ProviderName,
                observation.ExpectedStreamVersion.Value,
                observation.StreamVersion.Value,
                observation.Outcome.ToString(),
                observation.ProviderCommitDuration.TotalMilliseconds);
        }
    }

    private void LogRuntimeEvents(WorkflowRuntimeObservation observation)
    {
        foreach (var runtimeEvent in observation.Events)
        {
            if (runtimeEvent.StepPath is { } stepPath)
            {
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [OrcaCoreDiagnostics.StepPathKey] = stepPath,
                    [OrcaCoreDiagnostics.StepOperationIdKey] = runtimeEvent.StepOperationId?.ToString(),
                    [OrcaCoreDiagnostics.StepAttemptKey] = runtimeEvent.StepAttemptNumber,
                    [OrcaCoreDiagnostics.ErrorKindKey] = runtimeEvent.ErrorKind
                });
                DurableTelemetryLog.StepTransition(
                    logger,
                    runtimeEvent.EventType,
                    observation.InstanceId.ToString(),
                    stepPath,
                    runtimeEvent.StepOperationId?.ToString(),
                    runtimeEvent.StepAttemptNumber,
                    runtimeEvent.ErrorKind,
                    runtimeEvent.ErrorSummary);
            }

            if (runtimeEvent.WaitEventName is { } waitEventName)
            {
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [OrcaCoreDiagnostics.WaitEventNameKey] = waitEventName,
                    [OrcaCoreDiagnostics.CorrelationIdKey] = runtimeEvent.CorrelationId
                });
                DurableTelemetryLog.WaitTransition(
                    logger,
                    runtimeEvent.EventType,
                    observation.InstanceId.ToString(),
                    waitEventName,
                    runtimeEvent.CorrelationId);
            }

            if (runtimeEvent.TimerId is { } timerId)
            {
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [OrcaCoreDiagnostics.TimerIdKey] = timerId,
                    [OrcaCoreDiagnostics.TimerFireAtKey] = runtimeEvent.FireAt
                });
                DurableTelemetryLog.TimerTransition(
                    logger,
                    runtimeEvent.EventType,
                    observation.InstanceId.ToString(),
                    timerId,
                    runtimeEvent.FireAt);
            }

            foreach (var resource in runtimeEvent.ResourcePoolItems ?? [])
            {
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [OrcaCoreDiagnostics.ResourcePoolNameKey] = resource.PoolName,
                    [OrcaCoreDiagnostics.ResourceOwnerKey] = runtimeEvent.ResourceOwner,
                    [OrcaCoreDiagnostics.LeaseObligationIdKey] = runtimeEvent.LeaseObligationId,
                    [OrcaCoreDiagnostics.ResourceTicketIdKey] = resource.TicketId,
                    [OrcaCoreDiagnostics.ResourceOwnerGenerationKey] = resource.OwnerGeneration,
                    [OrcaCoreDiagnostics.ResourceActionKey] = runtimeEvent.EventType
                });
                DurableTelemetryLog.ResourcePoolTransition(
                    logger,
                    runtimeEvent.EventType,
                    observation.InstanceId.ToString(),
                    resource.PoolName,
                    runtimeEvent.ResourceOwner,
                    runtimeEvent.LeaseObligationId,
                    resource.TicketId,
                    resource.OwnerGeneration);
            }

            if (runtimeEvent.LifecycleEventName is { } lifecycleEventName)
            {
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [OrcaCoreDiagnostics.LifecycleEventNameKey] = lifecycleEventName,
                    [OrcaCoreDiagnostics.EventTypeKey] = runtimeEvent.EventType
                });
                DurableTelemetryLog.LifecycleTransition(
                    logger,
                    lifecycleEventName,
                    observation.InstanceId.ToString(),
                    runtimeEvent.EventType);
            }
        }
    }
}

internal static partial class DurableTelemetryLog
{
    private const int CommandCompletedEventId = 1001;
    private const int OutboxPumpCompletedEventId = 1101;
    private const int OutboxPermanentFailureEventId = 1102;
    private const int OutboxExceptionEventId = 1103;
    private const int StepTransitionEventId = 1201;
    private const int WaitTransitionEventId = 1301;
    private const int TimerTransitionEventId = 1302;
    private const int ProviderCommitCompletedEventId = 1401;
    private const int ProviderCommitConflictEventId = 1402;
    private const int ProviderCommitFailedEventId = 1403;
    private const int ResourcePoolTransitionEventId = 1601;
    private const int LifecycleTransitionEventId = 1701;

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
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} was marked as a permanent failure on attempt {Attempt} after {DurationMs} ms.")]
    internal static partial void OutboxPermanentFailure(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        int attempt,
        double durationMs);

    [LoggerMessage(
        EventId = OutboxExceptionEventId,
        Level = LogLevel.Error,
        Message = "Outbox record {OutboxRecordId} of kind {OutboxKind} threw {ExceptionType} on attempt {Attempt}: {ExceptionMessage}")]
    internal static partial void OutboxException(
        ILogger logger,
        string outboxKind,
        string outboxRecordId,
        int attempt,
        string exceptionType,
        string exceptionMessage);

    [LoggerMessage(
        EventId = StepTransitionEventId,
        Level = LogLevel.Debug,
        Message = "Durable step transition {EventType} for instance {InstanceId} at {StepPath}, operation {StepOperationId}, attempt {StepAttemptNumber}; error kind {ErrorKind}; summary {ErrorSummary}.")]
    internal static partial void StepTransition(
        ILogger logger,
        string eventType,
        string instanceId,
        string stepPath,
        string? stepOperationId,
        int? stepAttemptNumber,
        string? errorKind,
        string? errorSummary);

    [LoggerMessage(
        EventId = WaitTransitionEventId,
        Level = LogLevel.Information,
        Message = "Durable wait transition {EventType} for instance {InstanceId}, event {WaitEventName}, correlation {CorrelationId}.")]
    internal static partial void WaitTransition(
        ILogger logger,
        string eventType,
        string instanceId,
        string waitEventName,
        string? correlationId);

    [LoggerMessage(
        EventId = TimerTransitionEventId,
        Level = LogLevel.Information,
        Message = "Durable timer transition {EventType} for instance {InstanceId}, timer {TimerId}, fire at {FireAt}.")]
    internal static partial void TimerTransition(
        ILogger logger,
        string eventType,
        string instanceId,
        string timerId,
        DateTimeOffset? fireAt);

    [LoggerMessage(
        EventId = ProviderCommitCompletedEventId,
        Level = LogLevel.Information,
        Message = "Provider {ProviderName} commit completed with {Outcome}; expected stream version {ExpectedStreamVersion}, observed stream version {StreamVersion}, duration {DurationMs} ms.")]
    internal static partial void ProviderCommitCompleted(
        ILogger logger,
        string providerName,
        long expectedStreamVersion,
        long streamVersion,
        string outcome,
        double durationMs);

    [LoggerMessage(
        EventId = ProviderCommitConflictEventId,
        Level = LogLevel.Warning,
        Message = "Provider {ProviderName} commit conflicted; expected stream version {ExpectedStreamVersion}, observed stream version {StreamVersion}, duration {DurationMs} ms.")]
    internal static partial void ProviderCommitConflict(
        ILogger logger,
        string providerName,
        long expectedStreamVersion,
        long streamVersion,
        double durationMs);

    [LoggerMessage(
        EventId = ProviderCommitFailedEventId,
        Level = LogLevel.Error,
        Message = "Provider {ProviderName} commit failed at expected stream version {ExpectedStreamVersion} after {DurationMs} ms with {ExceptionType}: {ErrorSummary}.")]
    internal static partial void ProviderCommitFailed(
        ILogger logger,
        string providerName,
        long expectedStreamVersion,
        double durationMs,
        string exceptionType,
        string errorSummary,
        Exception exception);

    [LoggerMessage(
        EventId = ResourcePoolTransitionEventId,
        Level = LogLevel.Information,
        Message = "Resource-pool transition {EventType} for instance {InstanceId}, pool {PoolName}, owner {ResourceOwner}, obligation {LeaseObligationId}, ticket {TicketId}, owner generation {OwnerGeneration}.")]
    internal static partial void ResourcePoolTransition(
        ILogger logger,
        string eventType,
        string instanceId,
        string poolName,
        string? resourceOwner,
        string? leaseObligationId,
        string? ticketId,
        long? ownerGeneration);

    [LoggerMessage(
        EventId = LifecycleTransitionEventId,
        Level = LogLevel.Information,
        Message = "Durable lifecycle transition {LifecycleEventName} for instance {InstanceId} from event {EventType}.")]
    internal static partial void LifecycleTransition(
        ILogger logger,
        string lifecycleEventName,
        string instanceId,
        string eventType);
}
