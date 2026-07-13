using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using OrcaCore.Abstractions.Durable;

namespace OrcaCore.Abstractions.Serialization;

/// <summary>
/// Encodes durable workflow events using stable provider event type names.
/// </summary>
public static class WorkflowEventCodec
{
    private static readonly WorkflowEventCodecEntry[] Entries =
    [
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStartedEvent, "WorkflowStartedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowContinuedAsNewEvent, "WorkflowContinuedAsNewEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStepCompletedEvent, "WorkflowStepCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStepFailedEvent, "WorkflowStepFailedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowWaitRegisteredEvent, "WorkflowWaitRegisteredEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowWaitMatchedEvent, "WorkflowWaitMatchedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowWaitCancelledEvent, "WorkflowWaitCancelledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerCancelledEvent, "WorkflowTimerCancelledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResumeConsumedEvent, "WorkflowResumeConsumedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowParkedEvent, "WorkflowParkedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowUnparkedEvent, "WorkflowUnparkedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowContinuationAttemptFailedEvent, "WorkflowContinuationAttemptFailedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowContinuationAttemptResetEvent, "WorkflowContinuationAttemptResetEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerScheduledEvent, "WorkflowTimerScheduledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerFiredEvent, "WorkflowTimerFiredEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildScheduledEvent, "WorkflowChildScheduledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildrenScheduledEvent, "WorkflowChildrenScheduledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildrenDispatchedEvent, "WorkflowChildrenDispatchedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildCompletedEvent, "WorkflowChildCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenRecordedEvent, "WorkflowParentResumeTokenRecordedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenConsumedEvent, "WorkflowParentResumeTokenConsumedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildResidualIntentRecordedEvent, "WorkflowChildResidualIntentRecordedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildCompensationScheduledEvent, "WorkflowChildCompensationScheduledEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolAcquiredEvent, "WorkflowResourcePoolAcquiredEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolQueuedEvent, "WorkflowResourcePoolQueuedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolReleasedEvent, "WorkflowResourcePoolReleasedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStartedEvent, "WorkflowExternalJobStartedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobCompletedEvent, "WorkflowExternalJobCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobTimedOutEvent, "WorkflowExternalJobTimedOutEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStopRequestedEvent, "WorkflowExternalJobStopRequestedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerBufferedEvent, "WorkflowTimerBufferedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowPausedEvent, "WorkflowPausedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResumedEvent, "WorkflowResumedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryBufferedEvent, "WorkflowDeliveryBufferedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryDiscardedEvent, "WorkflowDeliveryDiscardedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowCompletedEvent, "WorkflowCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTerminalEvent, "WorkflowTerminalEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaForwardActionCompletedEvent, "SagaForwardActionCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaForwardActionTimedOutEvent, "SagaForwardActionTimedOutEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationRequestedEvent, "SagaCompensationRequestedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationStartedEvent, "SagaCompensationStartedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationCompletedEvent, "SagaCompensationCompletedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationFailedEvent, "SagaCompensationFailedEvent"),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaManualRecoveryRecordedEvent, "SagaManualRecoveryRecordedEvent")
    ];

    private static readonly IReadOnlyDictionary<Type, WorkflowEventCodecEntry> EntriesByClrType =
        Entries.ToDictionary(entry => entry.ClrType);

    private static readonly IReadOnlyDictionary<string, WorkflowEventCodecEntry> EntriesByEventType =
        Entries.ToDictionary(entry => entry.EventType, StringComparer.Ordinal);

    public static string ToEventType(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        return FindEntry(workflowEvent).EventType;
    }

    public static string Serialize(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        return FindEntry(workflowEvent).Serialize(workflowEvent);
    }

    public static WorkflowEvent Deserialize(string eventType, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        return EntriesByEventType.TryGetValue(eventType, out var entry)
            ? entry.Deserialize(payload)
            : throw new InvalidOperationException($"Workflow event type '{eventType}' is not supported.");
    }

    private static WorkflowEventCodecEntry FindEntry(WorkflowEvent workflowEvent)
    {
        return EntriesByClrType.TryGetValue(workflowEvent.GetType(), out var entry)
            ? entry
            : throw new InvalidOperationException(
                $"Workflow event '{workflowEvent.GetType().Name}' is not supported.");
    }

    /// <summary>
    /// Gets the frozen event-type name table. Names are persisted stream discriminators and
    /// must never change for an existing entry; a CLR type rename must keep its original name.
    /// </summary>
    public static IReadOnlyDictionary<string, string> EventTypeNamesByClrTypeName =>
        Entries.ToDictionary(entry => entry.ClrType.Name, entry => entry.EventType, StringComparer.Ordinal);

    private static WorkflowEventCodecEntry Entry<TEvent>(JsonTypeInfo<TEvent> jsonTypeInfo, string eventType)
        where TEvent : WorkflowEvent
    {
        return new WorkflowEventCodecEntry<TEvent>(jsonTypeInfo, eventType);
    }

    private static TEvent Required<TEvent>(TEvent? workflowEvent)
        where TEvent : WorkflowEvent
    {
        return workflowEvent ?? throw new JsonException("Workflow event payload could not be deserialized.");
    }

    private abstract class WorkflowEventCodecEntry(Type clrType, string eventType)
    {
        internal Type ClrType { get; } = clrType;

        internal string EventType { get; } = eventType;

        internal abstract string Serialize(WorkflowEvent workflowEvent);

        internal abstract WorkflowEvent Deserialize(string payload);
    }

    private sealed class WorkflowEventCodecEntry<TEvent>(
        JsonTypeInfo<TEvent> jsonTypeInfo,
        string eventType) : WorkflowEventCodecEntry(typeof(TEvent), eventType)
        where TEvent : WorkflowEvent
    {
        internal override string Serialize(WorkflowEvent workflowEvent)
        {
            return JsonSerializer.Serialize((TEvent)workflowEvent, jsonTypeInfo);
        }

        internal override WorkflowEvent Deserialize(string payload)
        {
            return Required(JsonSerializer.Deserialize(payload, jsonTypeInfo));
        }
    }
}
