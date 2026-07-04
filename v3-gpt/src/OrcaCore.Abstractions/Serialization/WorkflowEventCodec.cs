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
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStartedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowContinuedAsNewEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStepCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowStepFailedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowWaitRegisteredEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowWaitMatchedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerScheduledEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerFiredEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildScheduledEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildrenScheduledEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildrenDispatchedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenRecordedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowParentResumeTokenConsumedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildResidualIntentRecordedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowChildCompensationScheduledEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolAcquiredEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolQueuedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResourcePoolReleasedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStartedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobTimedOutEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowExternalJobStopRequestedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTimerBufferedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowPausedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowResumedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryBufferedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowDeliveryDiscardedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.WorkflowTerminalEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaForwardActionCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaForwardActionTimedOutEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationRequestedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationStartedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationCompletedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaCompensationFailedEvent),
        Entry(OrcaCoreJsonSerializerContext.Default.SagaManualRecoveryRecordedEvent)
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

    private static WorkflowEventCodecEntry Entry<TEvent>(JsonTypeInfo<TEvent> jsonTypeInfo)
        where TEvent : WorkflowEvent
    {
        return new WorkflowEventCodecEntry<TEvent>(jsonTypeInfo);
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
        JsonTypeInfo<TEvent> jsonTypeInfo) : WorkflowEventCodecEntry(typeof(TEvent), typeof(TEvent).Name)
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
