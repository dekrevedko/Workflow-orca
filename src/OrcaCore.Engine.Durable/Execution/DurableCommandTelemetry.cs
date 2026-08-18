using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
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
        StreamVersion? expectedStreamVersion = null,
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
                        ProviderCommitDuration = providerCommitDuration,
                        ExpectedStreamVersion = expectedStreamVersion ?? StreamVersion.Empty
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

    internal async Task ObserveProviderCommitFailedAsync(
        InstanceId instanceId,
        string commandType,
        string providerName,
        StreamVersion expectedStreamVersion,
        TimeSpan duration,
        Exception exception,
        CancellationToken cancellationToken)
    {
        try
        {
            await runtimeObserver.OnProviderCommitFailedAsync(
                new WorkflowProviderCommitFailureObservation(
                    instanceId,
                    commandType,
                    providerName,
                    expectedStreamVersion,
                    duration,
                    exception),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Runtime observations are diagnostics; observer failures must not change command results.
        }
    }

    internal static IReadOnlyList<WorkflowRuntimeEventObservation> CreateEventObservations(
        IReadOnlyList<DurableWorkflowEvent> events,
        DefinitionId? definitionId,
        IReadOnlyDictionary<WaitId, DurableActiveWait> activeWaitsById,
        TimeSpan stepDuration,
        WorkflowRuntimeTelemetryContext? context)
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
                        StepDuration: stepDuration,
                        StepOperationId: context?.StepOperationId,
                        StepAttemptNumber: context?.StepAttemptNumber),
                    WorkflowStepFailedEvent stepFailed => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        StepPath: stepFailed.StepPath,
                        ErrorKind: nameof(WorkflowStepFailedEvent),
                        ErrorSummary: stepFailed.ErrorSummary,
                        LifecycleEventName: "StepFailed",
                        StepDuration: stepDuration,
                        StepOperationId: context?.StepOperationId,
                        StepAttemptNumber: context?.StepAttemptNumber),
                    WorkflowWaitRegisteredEvent waitRegistered => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceSuspended",
                        WaitEventName: waitRegistered.EventName,
                        CorrelationId: waitRegistered.CorrelationId.ToString()),
                    WorkflowWaitMatchedEvent waitMatched => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceResumed",
                        WaitEventName: activeWaitsById.TryGetValue(waitMatched.WaitId, out var wait)
                            ? wait.EventName
                            : null,
                        CorrelationId: activeWaitsById.TryGetValue(waitMatched.WaitId, out wait)
                            ? wait.CorrelationId.ToString()
                            : null,
                        WaitDuration: activeWaitsById.TryGetValue(waitMatched.WaitId, out wait)
                            ? PositiveDuration(waitMatched.OccurredAt - wait.RegisteredAt)
                            : null),
                    WorkflowTimerScheduledEvent timerScheduled => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceSuspended",
                        TimerId: timerScheduled.TimerId.ToString(),
                        FireAt: timerScheduled.FireAt),
                    WorkflowTimerFiredEvent timerFired => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceResumed",
                        TimerId: timerFired.TimerId.ToString()),
                    WorkflowResourcePoolAcquiredEvent acquired => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        ResourceOwner: acquired.HolderKey,
                        ResourcePoolNames: PoolNames(acquired.Tickets.Select(ticket => ticket.PoolName)),
                        LeaseObligationId: context?.LeaseObligationId,
                        ResourcePoolItems: acquired.Tickets.Select(ResourcePoolItem).ToArray()),
                    WorkflowResourcePoolQueuedEvent queued => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        ResourceOwner: queued.HolderKey,
                        ResourcePoolNames: PoolNames(queued.Requirements.Select(requirement => requirement.PoolName)),
                        LeaseObligationId: context?.LeaseObligationId,
                        ResourcePoolItems: queued.Requirements
                            .Select(requirement => new WorkflowResourcePoolTelemetryItem(
                                requirement.PoolName,
                                TicketId: null,
                                OwnerGeneration: null))
                            .ToArray()),
                    WorkflowResourcePoolReleasedEvent released => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        ResourceOwner: released.HolderKey,
                        ResourcePoolNames: PoolNames(released.Tickets.Select(ticket => ticket.PoolName)),
                        LeaseObligationId: context?.LeaseObligationId,
                        ResourcePoolItems: released.Tickets.Select(ResourcePoolItem).ToArray()),
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
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity(
                OrcaCoreDiagnostics.EventApplyActivity);
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
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity(
                OrcaCoreDiagnostics.StepExecuteActivity);
            activity?.SetTag(OrcaCoreDiagnostics.StepPathKey, workflowEvent.StepPath);
            activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());
            activity?.SetTag(OrcaCoreDiagnostics.StepOperationIdKey, workflowEvent.StepOperationId?.ToString());
            activity?.SetTag(OrcaCoreDiagnostics.StepAttemptKey, workflowEvent.StepAttemptNumber);
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

    private static IReadOnlyList<string> PoolNames(IEnumerable<string> names)
    {
        return names
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorkflowResourcePoolTelemetryItem ResourcePoolItem(ResourcePoolTicket ticket) =>
        new(ticket.PoolName, ticket.TicketId.ToString("N"), ticket.ProviderGeneration);

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
