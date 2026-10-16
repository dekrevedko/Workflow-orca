using OrcaCore.Abstractions.Durable;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Driver;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

internal static class DurableWorkflowReplayApplier
{
    private static readonly ReplayHandler[] ReplayHandlers =
    [
        TryApplyLifecycle,
        TryApplyWaitsAndTimers,
        TryApplyResourcePool
    ];

    internal static void Apply(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(workflowEvent);

        BeginReplay(aggregate, workflowEvent);
        foreach (var handler in ReplayHandlers)
        {
            if (handler(aggregate, workflowEvent))
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Workflow event '{workflowEvent.GetType().Name}' is not supported by durable aggregate replay.");
    }

    internal static void ApplyStructuredEnvelopeStatus(
        DurableWorkflowAggregate aggregate,
        DurableCheckpointPayload envelope)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.ContentType != DurableExecutionEnvelopeV2.ContentType ||
            aggregate.Status is not (WorkflowStatus.Running or WorkflowStatus.Waiting))
        {
            return;
        }

        var execution = DurableFiberEnvelopeMapper.FromEnvelope(
            DurableExecutionEnvelopeV2.Deserialize(envelope.Payload));
        var derived = ExecutionStatusDeriver.Derive(WorkflowExecutionMode.Durable, execution);
        aggregate.Status = derived.Status ?? WorkflowStatus.Waiting;
    }

    private static void BeginReplay(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent)
    {
        aggregate.CreatedAt ??= workflowEvent.OccurredAt;
        aggregate.UpdatedAt = workflowEvent.OccurredAt;
        aggregate.StreamVersion = aggregate.StreamVersion.Next();
    }

    private static bool TryApplyLifecycle(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent)
    {
        switch (workflowEvent)
        {
            case WorkflowStartedEvent started:
                aggregate.ParentInstanceId = started.ParentInstanceId;
                aggregate.RootInstanceId = started.RootInstanceId ?? started.InstanceId;
                aggregate.DefinitionId = started.DefinitionId;
                aggregate.DefinitionVersion = started.DefinitionVersion;
                aggregate.StartInputContentType = started.InputContentType;
                aggregate.StartInputPayload = started.InputPayload;
                aggregate.Status = WorkflowStatus.Running;
                return true;
            case WorkflowParkedEvent parked:
                aggregate.Status = WorkflowStatus.Waiting;
                aggregate.ParkReason = parked.Reason;
                aggregate.ErrorSummary = parked.ErrorSummary;
                return true;
            case WorkflowUnparkedEvent:
                aggregate.ParkReason = null;
                aggregate.ErrorSummary = null;
                ClearContinuationFailures(aggregate);
                aggregate.Status = aggregate.WaitState.HasActiveWaits || aggregate.TimerState.HasActiveTimers
                    ? WorkflowStatus.Waiting
                    : WorkflowStatus.Running;
                return true;
            case WorkflowContinuationAttemptFailedEvent attemptFailed:
                aggregate.ContinuationFailureCount = attemptFailed.AttemptCount;
                aggregate.ContinuationFailurePositionStreamVersion = attemptFailed.PositionStreamVersion;
                aggregate.ContinuationRetryNotBefore = attemptFailed.NextEligibleAt;
                return true;
            case WorkflowContinuationAttemptResetEvent:
                ClearContinuationFailures(aggregate);
                return true;
            case WorkflowContinuedAsNewEvent continuedAsNew:
                aggregate.ContinueAsNewGeneration = continuedAsNew.Generation;
                aggregate.Status = WorkflowStatus.Running;
                aggregate.ErrorSummary = null;
                aggregate.OutcomeName = null;
                ClearContinuationFailures(aggregate);
                aggregate.TimerState.Clear();
                aggregate.WaitState.Clear();
                aggregate.ResourcePoolState.Clear();
                return true;
            case WorkflowStepCompletedEvent stepCompleted:
                aggregate.LastStepPath = stepCompleted.StepPath;
                if (aggregate.Status is not WorkflowStatus.Waiting)
                {
                    aggregate.Status = WorkflowStatus.Running;
                }

                return true;
            case WorkflowStepFailedEvent stepFailed:
                aggregate.LastStepPath = stepFailed.StepPath;
                aggregate.ErrorSummary = stepFailed.ErrorSummary;
                aggregate.Status = WorkflowStatus.Failed;
                aggregate.TimerState.Clear();
                aggregate.WaitState.Clear();
                return true;
            case WorkflowCompletedEvent completed:
                aggregate.OutcomeName = completed.OutcomeName;
                aggregate.Status = WorkflowStatus.Completed;
                ClearActiveWork(aggregate);
                return true;
            case WorkflowCancellationRequestedEvent:
                aggregate.Status = WorkflowStatus.CancellationRequested;
                return true;
            case WorkflowTerminalEvent terminal:
                aggregate.Status = terminal.Status;
                aggregate.ErrorSummary = terminal.ErrorSummary ?? aggregate.ErrorSummary;
                if (aggregate.IsTerminal)
                {
                    ClearActiveWork(aggregate);
                }

                return true;
            default:
                return false;
        }
    }

    private static bool TryApplyWaitsAndTimers(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent)
    {
        switch (workflowEvent)
        {
            case WorkflowWaitRegisteredEvent waitRegistered:
                aggregate.WaitState.Apply(waitRegistered);
                aggregate.Status = WorkflowStatus.Waiting;
                return true;
            case WorkflowWaitMatchedEvent waitMatched:
                aggregate.WaitState.Apply(waitMatched);
                aggregate.Status = aggregate.WaitState.HasActiveWaits ? WorkflowStatus.Waiting : WorkflowStatus.Running;
                return true;
            case WorkflowWaitCancelledEvent waitCancelled:
                aggregate.WaitState.Apply(waitCancelled);
                aggregate.Status = aggregate.WaitState.HasActiveWaits || aggregate.TimerState.HasActiveTimers
                    ? WorkflowStatus.Waiting
                    : WorkflowStatus.Running;
                return true;
            case WorkflowTimerCancelledEvent timerCancelled:
                aggregate.TimerState.Apply(timerCancelled);
                aggregate.Status = aggregate.WaitState.HasActiveWaits || aggregate.TimerState.HasActiveTimers
                    ? WorkflowStatus.Waiting
                    : WorkflowStatus.Running;
                return true;
            case WorkflowResumeConsumedEvent resumeConsumed:
                aggregate.WaitState.Apply(resumeConsumed);
                return true;
            case WorkflowTimerScheduledEvent timerScheduled:
                aggregate.TimerState.Apply(timerScheduled);
                aggregate.Status = WorkflowStatus.Waiting;
                return true;
            case WorkflowTimerFiredEvent timerFired:
                aggregate.TimerState.Apply(timerFired);
                aggregate.Status = !aggregate.WaitState.HasActiveWaits && !aggregate.TimerState.HasActiveTimers
                    ? WorkflowStatus.Running
                    : WorkflowStatus.Waiting;
                return true;
            default:
                return false;
        }
    }

    private static bool TryApplyResourcePool(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent)
    {
        switch (workflowEvent)
        {
            case WorkflowResourcePoolAcquiredEvent resourcePoolAcquired:
                aggregate.ResourcePoolState.Apply(resourcePoolAcquired);
                aggregate.Status = WorkflowStatus.Running;
                return true;
            case WorkflowResourcePoolQueuedEvent resourcePoolQueued:
                aggregate.ApplyResourcePoolReplayEffects(aggregate.ResourcePoolState.Apply(resourcePoolQueued));
                aggregate.Status = WorkflowStatus.Waiting;
                return true;
            case WorkflowResourcePoolReleasedEvent resourcePoolReleased:
                aggregate.ResourcePoolState.Apply(resourcePoolReleased);
                return true;
            default:
                return false;
        }
    }

    private static void ClearActiveWork(DurableWorkflowAggregate aggregate)
    {
        aggregate.TimerState.Clear();
        aggregate.WaitState.Clear();
        aggregate.ResourcePoolState.Clear();
    }

    private static void ClearContinuationFailures(DurableWorkflowAggregate aggregate)
    {
        aggregate.ContinuationFailureCount = 0;
        aggregate.ContinuationFailurePositionStreamVersion = null;
        aggregate.ContinuationRetryNotBefore = null;
    }

    private delegate bool ReplayHandler(DurableWorkflowAggregate aggregate, DurableWorkflowEvent workflowEvent);
}
