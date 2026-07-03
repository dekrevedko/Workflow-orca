using System.Text.Json;
using OrcaCore.Abstractions.Durable;

namespace OrcaCore.Abstractions.Serialization;

/// <summary>
/// Encodes durable workflow events using stable provider event type names.
/// </summary>
public static class WorkflowEventCodec
{
    public static string ToEventType(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        return workflowEvent switch
        {
            WorkflowStartedEvent => nameof(WorkflowStartedEvent),
            WorkflowContinuedAsNewEvent => nameof(WorkflowContinuedAsNewEvent),
            WorkflowStepCompletedEvent => nameof(WorkflowStepCompletedEvent),
            WorkflowStepFailedEvent => nameof(WorkflowStepFailedEvent),
            WorkflowWaitRegisteredEvent => nameof(WorkflowWaitRegisteredEvent),
            WorkflowWaitMatchedEvent => nameof(WorkflowWaitMatchedEvent),
            WorkflowTimerScheduledEvent => nameof(WorkflowTimerScheduledEvent),
            WorkflowTimerFiredEvent => nameof(WorkflowTimerFiredEvent),
            WorkflowChildScheduledEvent => nameof(WorkflowChildScheduledEvent),
            WorkflowChildrenScheduledEvent => nameof(WorkflowChildrenScheduledEvent),
            WorkflowChildrenDispatchedEvent => nameof(WorkflowChildrenDispatchedEvent),
            WorkflowChildCompletedEvent => nameof(WorkflowChildCompletedEvent),
            WorkflowParentResumeTokenRecordedEvent => nameof(WorkflowParentResumeTokenRecordedEvent),
            WorkflowParentResumeTokenConsumedEvent => nameof(WorkflowParentResumeTokenConsumedEvent),
            WorkflowChildResidualIntentRecordedEvent => nameof(WorkflowChildResidualIntentRecordedEvent),
            WorkflowChildCompensationScheduledEvent => nameof(WorkflowChildCompensationScheduledEvent),
            WorkflowResourcePoolAcquiredEvent => nameof(WorkflowResourcePoolAcquiredEvent),
            WorkflowResourcePoolQueuedEvent => nameof(WorkflowResourcePoolQueuedEvent),
            WorkflowResourcePoolReleasedEvent => nameof(WorkflowResourcePoolReleasedEvent),
            WorkflowExternalJobStartedEvent => nameof(WorkflowExternalJobStartedEvent),
            WorkflowExternalJobCompletedEvent => nameof(WorkflowExternalJobCompletedEvent),
            WorkflowExternalJobTimedOutEvent => nameof(WorkflowExternalJobTimedOutEvent),
            WorkflowExternalJobStopRequestedEvent => nameof(WorkflowExternalJobStopRequestedEvent),
            WorkflowTimerBufferedEvent => nameof(WorkflowTimerBufferedEvent),
            WorkflowPausedEvent => nameof(WorkflowPausedEvent),
            WorkflowResumedEvent => nameof(WorkflowResumedEvent),
            WorkflowDeliveryBufferedEvent => nameof(WorkflowDeliveryBufferedEvent),
            WorkflowDeliveryDiscardedEvent => nameof(WorkflowDeliveryDiscardedEvent),
            WorkflowCompletedEvent => nameof(WorkflowCompletedEvent),
            WorkflowTerminalEvent => nameof(WorkflowTerminalEvent),
            SagaForwardActionCompletedEvent => nameof(SagaForwardActionCompletedEvent),
            SagaForwardActionTimedOutEvent => nameof(SagaForwardActionTimedOutEvent),
            SagaCompensationRequestedEvent => nameof(SagaCompensationRequestedEvent),
            SagaCompensationStartedEvent => nameof(SagaCompensationStartedEvent),
            SagaCompensationCompletedEvent => nameof(SagaCompensationCompletedEvent),
            SagaCompensationFailedEvent => nameof(SagaCompensationFailedEvent),
            SagaManualRecoveryRecordedEvent => nameof(SagaManualRecoveryRecordedEvent),
            _ => throw new InvalidOperationException(
                $"Workflow event '{workflowEvent.GetType().Name}' is not supported.")
        };
    }

    public static string Serialize(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        return workflowEvent switch
        {
            WorkflowStartedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowStartedEvent),
            WorkflowContinuedAsNewEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowContinuedAsNewEvent),
            WorkflowStepCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowStepCompletedEvent),
            WorkflowStepFailedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowStepFailedEvent),
            WorkflowWaitRegisteredEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowWaitRegisteredEvent),
            WorkflowWaitMatchedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowWaitMatchedEvent),
            WorkflowTimerScheduledEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowTimerScheduledEvent),
            WorkflowTimerFiredEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowTimerFiredEvent),
            WorkflowChildScheduledEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildScheduledEvent),
            WorkflowChildrenScheduledEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildrenScheduledEvent),
            WorkflowChildrenDispatchedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildrenDispatchedEvent),
            WorkflowChildCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildCompletedEvent),
            WorkflowParentResumeTokenRecordedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenRecordedEvent),
            WorkflowParentResumeTokenConsumedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenConsumedEvent),
            WorkflowChildResidualIntentRecordedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildResidualIntentRecordedEvent),
            WorkflowChildCompensationScheduledEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowChildCompensationScheduledEvent),
            WorkflowResourcePoolAcquiredEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolAcquiredEvent),
            WorkflowResourcePoolQueuedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolQueuedEvent),
            WorkflowResourcePoolReleasedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolReleasedEvent),
            WorkflowExternalJobStartedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStartedEvent),
            WorkflowExternalJobCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobCompletedEvent),
            WorkflowExternalJobTimedOutEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobTimedOutEvent),
            WorkflowExternalJobStopRequestedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStopRequestedEvent),
            WorkflowTimerBufferedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowTimerBufferedEvent),
            WorkflowPausedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowPausedEvent),
            WorkflowResumedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowResumedEvent),
            WorkflowDeliveryBufferedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryBufferedEvent),
            WorkflowDeliveryDiscardedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryDiscardedEvent),
            WorkflowCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowCompletedEvent),
            WorkflowTerminalEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.WorkflowTerminalEvent),
            SagaForwardActionCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaForwardActionCompletedEvent),
            SagaForwardActionTimedOutEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaForwardActionTimedOutEvent),
            SagaCompensationRequestedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaCompensationRequestedEvent),
            SagaCompensationStartedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaCompensationStartedEvent),
            SagaCompensationCompletedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaCompensationCompletedEvent),
            SagaCompensationFailedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaCompensationFailedEvent),
            SagaManualRecoveryRecordedEvent typed => JsonSerializer.Serialize(typed, OrcaCoreJsonSerializerContext.Default.SagaManualRecoveryRecordedEvent),
            _ => throw new InvalidOperationException(
                $"Workflow event '{workflowEvent.GetType().Name}' is not supported.")
        };
    }

    public static WorkflowEvent Deserialize(string eventType, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        return eventType switch
        {
            nameof(WorkflowStartedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowStartedEvent)),
            nameof(WorkflowContinuedAsNewEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowContinuedAsNewEvent)),
            nameof(WorkflowStepCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowStepCompletedEvent)),
            nameof(WorkflowStepFailedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowStepFailedEvent)),
            nameof(WorkflowWaitRegisteredEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowWaitRegisteredEvent)),
            nameof(WorkflowWaitMatchedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowWaitMatchedEvent)),
            nameof(WorkflowTimerScheduledEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowTimerScheduledEvent)),
            nameof(WorkflowTimerFiredEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowTimerFiredEvent)),
            nameof(WorkflowChildScheduledEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildScheduledEvent)),
            nameof(WorkflowChildrenScheduledEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildrenScheduledEvent)),
            nameof(WorkflowChildrenDispatchedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildrenDispatchedEvent)),
            nameof(WorkflowChildCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildCompletedEvent)),
            nameof(WorkflowParentResumeTokenRecordedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenRecordedEvent)),
            nameof(WorkflowParentResumeTokenConsumedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenConsumedEvent)),
            nameof(WorkflowChildResidualIntentRecordedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildResidualIntentRecordedEvent)),
            nameof(WorkflowChildCompensationScheduledEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowChildCompensationScheduledEvent)),
            nameof(WorkflowResourcePoolAcquiredEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolAcquiredEvent)),
            nameof(WorkflowResourcePoolQueuedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolQueuedEvent)),
            nameof(WorkflowResourcePoolReleasedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolReleasedEvent)),
            nameof(WorkflowExternalJobStartedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStartedEvent)),
            nameof(WorkflowExternalJobCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobCompletedEvent)),
            nameof(WorkflowExternalJobTimedOutEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobTimedOutEvent)),
            nameof(WorkflowExternalJobStopRequestedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStopRequestedEvent)),
            nameof(WorkflowTimerBufferedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowTimerBufferedEvent)),
            nameof(WorkflowPausedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowPausedEvent)),
            nameof(WorkflowResumedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowResumedEvent)),
            nameof(WorkflowDeliveryBufferedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryBufferedEvent)),
            nameof(WorkflowDeliveryDiscardedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryDiscardedEvent)),
            nameof(WorkflowCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowCompletedEvent)),
            nameof(WorkflowTerminalEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.WorkflowTerminalEvent)),
            nameof(SagaForwardActionCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaForwardActionCompletedEvent)),
            nameof(SagaForwardActionTimedOutEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaForwardActionTimedOutEvent)),
            nameof(SagaCompensationRequestedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaCompensationRequestedEvent)),
            nameof(SagaCompensationStartedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaCompensationStartedEvent)),
            nameof(SagaCompensationCompletedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaCompensationCompletedEvent)),
            nameof(SagaCompensationFailedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaCompensationFailedEvent)),
            nameof(SagaManualRecoveryRecordedEvent) => Required(JsonSerializer.Deserialize(payload, OrcaCoreJsonSerializerContext.Default.SagaManualRecoveryRecordedEvent)),
            _ => throw new InvalidOperationException($"Workflow event type '{eventType}' is not supported.")
        };
    }

    private static TEvent Required<TEvent>(TEvent? workflowEvent)
        where TEvent : WorkflowEvent
    {
        return workflowEvent ?? throw new JsonException("Workflow event payload could not be deserialized.");
    }
}
