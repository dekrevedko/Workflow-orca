using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Diagnostics;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Execution;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

/// <summary>
/// Diagnostics sink for command processing: maps committed events to runtime observations
/// and spans, and forwards completions to the <see cref="IWorkflowRuntimeObserver"/>.
/// Observer failures never change command results.
/// </summary>
internal sealed class DurableCommandTelemetry(IWorkflowRuntimeObserver runtimeObserver)
{
    internal async Task<DurableCommandResult> ObserveCommandCompletedAsync(
        InstanceId instanceId,
        DurableCommandResult result,
        int eventCount,
        bool checkpointWritten,
        EventId? inboxEventId,
        CancellationToken cancellationToken,
        string commandType,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        TimeSpan duration,
        IReadOnlyList<WorkflowRuntimeEventObservation>? events = null,
        bool providerCommitAttempted = false,
        string providerName = "unknown",
        TimeSpan providerCommitDuration = default,
        bool inboxDuplicate = false)
    {
        try
        {
            await runtimeObserver
                .OnCommandCompletedAsync(
                    new WorkflowRuntimeObservation(
                        ToObservationKind(result.Outcome),
                        instanceId,
                        result.Outcome,
                        result.StreamVersion,
                        eventCount,
                        checkpointWritten,
                        result.Evicted,
                        inboxEventId,
                        result.Message,
                        commandType,
                        definitionId,
                        definitionVersion,
                        status,
                        duration)
                    {
                        Events = events ?? [],
                        InboxDuplicate = inboxDuplicate,
                        ProviderCommitAttempted = providerCommitAttempted,
                        ProviderName = providerName,
                        ProviderCommitDuration = providerCommitDuration
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Runtime observations are diagnostics; observer failures must not change command results.
        }

        return result;
    }

    internal static IReadOnlyList<WorkflowRuntimeEventObservation> CreateEventObservations(
        IReadOnlyList<DurableWorkflowEvent> events,
        DefinitionId? definitionId,
        IReadOnlyDictionary<WaitId, DurableActiveWait> activeWaitsById,
        TimeSpan stepDuration)
    {
        return events
            .Select(workflowEvent =>
            {
                var eventDefinitionId = workflowEvent is WorkflowStartedEvent started
                    ? started.DefinitionId
                    : definitionId;
                var eventType = WorkflowEventCodec.ToEventType(workflowEvent);
                return workflowEvent switch
                {
                    WorkflowStepCompletedEvent stepCompleted => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        StepPath: stepCompleted.StepPath,
                        LifecycleEventName: "StepCompleted",
                        StepDuration: stepDuration),
                    WorkflowStepFailedEvent stepFailed => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        StepPath: stepFailed.StepPath,
                        ErrorKind: nameof(WorkflowStepFailedEvent),
                        LifecycleEventName: "StepFailed",
                        StepDuration: stepDuration),
                    WorkflowWaitMatchedEvent waitMatched => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceResumed",
                        WaitEventName: activeWaitsById.TryGetValue(waitMatched.WaitId, out var wait)
                            ? wait.EventName
                            : null,
                        WaitDuration: activeWaitsById.TryGetValue(waitMatched.WaitId, out wait)
                            ? PositiveDuration(waitMatched.OccurredAt - wait.RegisteredAt)
                            : null),
                    WorkflowParkedEvent parked => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceParked",
                        ParkReason: parked.Reason),
                    _ => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: ToLifecycleEventName(workflowEvent))
                };
            })
            .ToArray();
    }

    internal static void RecordEventSpans(
        IReadOnlyList<WorkflowRuntimeEventObservation> events,
        InstanceId instanceId)
    {
        foreach (var workflowEvent in events)
        {
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.event.apply");
            activity?.SetTag(OrcaCoreDiagnostics.EventTypeKey, workflowEvent.EventType);
            activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());
            if (workflowEvent.DefinitionId is { } definitionId)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
            }
        }
    }

    internal static void RecordStepSpans(
        IReadOnlyList<WorkflowRuntimeEventObservation> events,
        InstanceId instanceId)
    {
        foreach (var workflowEvent in events.Where(workflowEvent => workflowEvent.StepPath is not null))
        {
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.step.execute");
            activity?.SetTag(OrcaCoreDiagnostics.StepPathKey, workflowEvent.StepPath);
            activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());
            if (workflowEvent.DefinitionId is { } definitionId)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
            }

            if (workflowEvent.ErrorKind is { } errorKind)
            {
                activity?.SetTag(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
                activity?.SetStatus(ActivityStatusCode.Error, errorKind);
            }
        }
    }

    internal static string ProviderName(object provider)
    {
        var name = provider.GetType().Name;
        return name
            .Replace("WorkflowProvider", string.Empty, StringComparison.Ordinal)
            .Replace("WorkflowStore", string.Empty, StringComparison.Ordinal)
            .Replace("EventStore", string.Empty, StringComparison.Ordinal);
    }

    private static string? ToLifecycleEventName(DurableWorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent => "InstanceStarted",
            WorkflowContinuedAsNewEvent => "InstanceContinuedAsNew",
            WorkflowWaitRegisteredEvent => "InstanceSuspended",
            WorkflowTimerScheduledEvent => "InstanceSuspended",
            WorkflowTimerFiredEvent => "InstanceResumed",
            WorkflowParkedEvent => "InstanceParked",
            WorkflowUnparkedEvent => "InstanceUnparked",
            WorkflowCompletedEvent => "InstanceCompleted",
            WorkflowCancellationRequestedEvent => "InstanceCancellationRequested",
            WorkflowTerminalEvent { Status: WorkflowStatus.Failed } => "InstanceFailed",
            WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled } => "InstanceCancelled",
            WorkflowTerminalEvent { Status: WorkflowStatus.Terminated } => "InstanceTerminated",
            _ => null
        };
    }

    private static TimeSpan PositiveDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static WorkflowRuntimeObservationKind ToObservationKind(DurableCommandOutcome outcome)
    {
        return outcome switch
        {
            DurableCommandOutcome.Committed => WorkflowRuntimeObservationKind.CommandCommitted,
            DurableCommandOutcome.Conflict => WorkflowRuntimeObservationKind.CommandConflict,
            DurableCommandOutcome.Evicted => WorkflowRuntimeObservationKind.CommandEvicted,
            DurableCommandOutcome.Poisoned => WorkflowRuntimeObservationKind.CommandPoisoned,
            DurableCommandOutcome.NoOp => WorkflowRuntimeObservationKind.CommandNoOp,
            _ => throw new UnreachableException()
        };
    }
}
