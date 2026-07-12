using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowInstance<TState> : IWorkflowInstance
{
    private readonly List<RuntimeWaitRecord> activeWaits = [];
    private readonly HashSet<EventId> consumedEventIds = [];
    private readonly Queue<EventId> consumedEventOrder = [];
    private readonly HashSet<WaitSignature> consumedWaits = [];
    private readonly Dictionary<WaitSignature, DateTimeOffset> timedOutWaits = [];
    private readonly List<CompositionBranchOutcomeSnapshot> compositionOutcomes = [];
    private readonly List<ForEachGroupRecord> forEachGroups = [];
    private readonly List<LifecycleEventSnapshot> lifecycleEvents = [];
    private readonly List<EventEnvelope> pendingEvents = [];
    private readonly List<RuntimeTimerRecord> activeTimers = [];
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly object snapshotGate = new();
    private readonly SemaphoreSlim stateAccess = new(1, 1);
    private readonly int maxConsumedEventIds;
    private readonly int maxLifecycleEvents;
    private readonly int maxPendingEvents;
    private ActiveStepSnapshot? activeStep;
    private readonly Queue<Func<CancellationToken, Task>> yieldContinuations = [];
    private bool hasStuckStep;
    private bool isStuck;
    private DateTimeOffset currentStatusEnteredAt;
    private DateTimeOffset lastActiveAt;
    private DateTimeOffset? stuckDetectedAt;
    private string? stuckStepPath;
    private WorkflowInstanceSnapshot? publishedSnapshot;
    private TState? publishedState;
    private bool hasPublishedState;

    internal WorkflowInstance(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TState state,
        DateTimeOffset createdAt,
        int maxPendingEvents = 1_024,
        int maxConsumedEventIds = 10_000,
        int maxLifecycleEvents = 10_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPendingEvents);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConsumedEventIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLifecycleEvents);

        this.maxPendingEvents = maxPendingEvents;
        this.maxConsumedEventIds = maxConsumedEventIds;
        this.maxLifecycleEvents = maxLifecycleEvents;
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        CurrentStatusEnteredAt = createdAt;
        LastActiveAt = createdAt;
        Status = WorkflowStatus.Running;
        RecordLifecycleEvent("InstanceStarted", null, WorkflowStatus.Running, createdAt);
        ToSnapshot();
    }

    internal InstanceId InstanceId { get; }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    internal TState State { get; }

    internal WorkflowStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal DateTimeOffset CurrentStatusEnteredAt
    {
        get => currentStatusEnteredAt;
        private set => currentStatusEnteredAt = value;
    }

    internal DateTimeOffset LastActiveAt
    {
        get => lastActiveAt;
        private set => lastActiveAt = value;
    }

    internal WorkflowErrorDetails? ErrorDetails { get; private set; }

    internal string? EndOutcomeName { get; private set; }

    internal bool HasUnresolvedRuntimeWork =>
        activeWaits.Count > 0 ||
        pendingEvents.Count > 0 ||
        activeTimers.Count > 0 ||
        yieldContinuations.Count > 0;

    internal bool HasActiveWait(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return activeWaits.Any(wait => wait.Matches(envelope));
    }

    internal RuntimeWaitRecord EnterWait(
        string eventName,
        CorrelationId correlationId,
        BranchId? branchId,
        DateTimeOffset registeredAt,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync)
    {
        Status = WorkflowStatus.Waiting;
        UpdatedAt = registeredAt;
        CurrentStatusEnteredAt = registeredAt;
        var wait = new RuntimeWaitRecord(eventName, correlationId, branchId, registeredAt, resumeAsync);
        activeWaits.Add(wait);
        return wait;
    }

    internal async Task<WorkflowInstanceSnapshot> RaiseEventAsync(
        EventEnvelope envelope,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (HasSeen(envelope.EventId))
        {
            return ToSnapshot();
        }

        if (HasTimedOutWait(envelope))
        {
            return ToSnapshot();
        }

        if (LifecycleMachine.TerminalStatuses.Contains(Status))
        {
            if (HasConsumedWait(envelope))
            {
                return ToSnapshot();
            }

            throw new WorkflowLifecycleException(
                $"Cannot raise event for workflow instance '{InstanceId}' because status '{Status}' is terminal.");
        }

        var matchingWaits = activeWaits.Where(candidate => candidate.Matches(envelope)).ToArray();
        if (matchingWaits.Length > 1 && string.IsNullOrWhiteSpace(envelope.BranchId))
        {
            throw new WorkflowRoutingException(
                $"Event '{envelope.EventName}' with correlation '{envelope.CorrelationId}' matches multiple branch waits; provide BranchId.");
        }

        var wait = matchingWaits.FirstOrDefault();
        if (wait is null)
        {
            if (HasConsumedWait(envelope))
            {
                return ToSnapshot();
            }

            if (pendingEvents.Count >= maxPendingEvents)
            {
                throw new WorkflowRoutingException(
                    $"Workflow instance '{InstanceId}' pending-event mailbox reached its configured limit " +
                    $"of {maxPendingEvents} events.");
            }

            pendingEvents.Add(envelope);
            return ToSnapshot();
        }

        return await ResumeWaitAsync(wait, envelope, processedAt, removePendingAfterCommit: false, cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<WorkflowInstanceSnapshot> MatchPendingEventAsync(
        RuntimeWaitRecord wait,
        DateTimeOffset processedAt,
        CancellationToken cancellationToken)
    {
        var envelope = pendingEvents.FirstOrDefault(wait.Matches);
        if (envelope is null)
        {
            return ToSnapshot();
        }

        return await ResumeWaitAsync(wait, envelope, processedAt, removePendingAfterCommit: true, cancellationToken)
            .ConfigureAwait(false);
    }

    internal RuntimeTimerRecord EnterDelay(BranchId? branchId, DateTimeOffset registeredAt)
    {
        Status = WorkflowStatus.Waiting;
        UpdatedAt = registeredAt;
        CurrentStatusEnteredAt = registeredAt;
        var timer = new RuntimeTimerRecord(branchId, registeredAt);
        activeTimers.Add(timer);
        return timer;
    }

    internal async Task<WorkflowInstanceSnapshot> FireDelayAsync(
        RuntimeTimerRecord timer,
        DateTimeOffset firedAt,
        Func<CancellationToken, Task> resumeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        if (!activeTimers.Remove(timer))
        {
            return ToSnapshot();
        }

        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = WorkflowStatus.Running;
        UpdatedAt = firedAt;
        CurrentStatusEnteredAt = firedAt;
        LastActiveAt = firedAt;

        try
        {
            await resumeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!activeTimers.Contains(timer))
            {
                activeTimers.Add(timer);
            }

            Status = WorkflowStatus.Waiting;
            UpdatedAt = timer.RegisteredAt;
            CurrentStatusEnteredAt = timer.RegisteredAt;
            throw;
        }

        if (Status == WorkflowStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = WorkflowStatus.Waiting;
            CurrentStatusEnteredAt = firedAt;
        }

        return ToSnapshot();
    }

    internal async Task<WorkflowInstanceSnapshot> FireWaitTimeoutAsync(
        RuntimeWaitRecord wait,
        DateTimeOffset firedAt,
        Func<CancellationToken, Task> resumeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wait);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        if (!activeWaits.Remove(wait))
        {
            return ToSnapshot();
        }

        wait.MarkMatched();
        var signature = new WaitSignature(wait.EventName, wait.CorrelationId, wait.BranchId);
        var consumedWaitAdded = consumedWaits.Add(signature);
        var hadPreviousTimeout = timedOutWaits.TryGetValue(signature, out var previousTimeout);
        timedOutWaits[signature] = firedAt;
        var removedPendingEvents = pendingEvents
            .Where(candidate => WaitSignature.From(candidate) == signature)
            .ToArray();
        pendingEvents.RemoveAll(candidate => WaitSignature.From(candidate) == signature);
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = WorkflowStatus.Running;
        UpdatedAt = firedAt;
        CurrentStatusEnteredAt = firedAt;
        LastActiveAt = firedAt;

        try
        {
            await resumeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            wait.MarkActive();
            activeWaits.Add(wait);
            if (consumedWaitAdded)
            {
                consumedWaits.Remove(signature);
            }

            if (hadPreviousTimeout)
            {
                timedOutWaits[signature] = previousTimeout;
            }
            else
            {
                timedOutWaits.Remove(signature);
            }

            foreach (var pendingEvent in removedPendingEvents)
            {
                if (pendingEvents.All(candidate => candidate.EventId != pendingEvent.EventId))
                {
                    pendingEvents.Add(pendingEvent);
                }
            }

            Status = WorkflowStatus.Waiting;
            UpdatedAt = wait.RegisteredAt;
            CurrentStatusEnteredAt = wait.RegisteredAt;
            throw;
        }

        if (Status == WorkflowStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = WorkflowStatus.Waiting;
            CurrentStatusEnteredAt = firedAt;
        }

        return ToSnapshot();
    }

    private async Task<WorkflowInstanceSnapshot> ResumeWaitAsync(
        RuntimeWaitRecord wait,
        EventEnvelope envelope,
        DateTimeOffset processedAt,
        bool removePendingAfterCommit,
        CancellationToken cancellationToken)
    {
        wait.MarkMatched();
        activeWaits.Remove(wait);
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = WorkflowStatus.Running;
        UpdatedAt = processedAt;
        CurrentStatusEnteredAt = processedAt;
        LastActiveAt = processedAt;

        var removedPendingEvent = removePendingAfterCommit &&
            pendingEvents.RemoveAll(candidate => candidate.EventId == envelope.EventId) > 0;

        try
        {
            await wait.ResumeAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            wait.MarkActive();
            if (!activeWaits.Contains(wait))
            {
                activeWaits.Add(wait);
            }

            if (removedPendingEvent && pendingEvents.All(candidate => candidate.EventId != envelope.EventId))
            {
                pendingEvents.Add(envelope);
            }

            Status = WorkflowStatus.Waiting;
            UpdatedAt = wait.RegisteredAt;
            CurrentStatusEnteredAt = wait.RegisteredAt;
            throw;
        }

        wait.CancelLoser();
        RecordConsumedEvent(envelope.EventId);
        consumedWaits.Add(new WaitSignature(wait.EventName, wait.CorrelationId, wait.BranchId));
        if (Status == WorkflowStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = WorkflowStatus.Waiting;
            CurrentStatusEnteredAt = processedAt;
        }

        return ToSnapshot();
    }

    internal void RecordCompositionBranchOutcome(
        string compositionId,
        BranchId branchId,
        string status,
        DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(compositionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        compositionOutcomes.RemoveAll(outcome =>
            string.Equals(outcome.CompositionId, compositionId, StringComparison.Ordinal) &&
            string.Equals(outcome.BranchId, branchId.ToString(), StringComparison.Ordinal));
        compositionOutcomes.Add(new CompositionBranchOutcomeSnapshot
        {
            CompositionId = compositionId,
            BranchId = branchId.ToString(),
            Status = status,
            RecordedAt = recordedAt
        });
        UpdatedAt = recordedAt;
    }

    internal ForEachGroupRecord RecordForEachGroup(
        string groupId,
        IReadOnlyList<ForEachWorkItemSnapshot> workItems,
        int? maxConcurrency,
        DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        ArgumentNullException.ThrowIfNull(workItems);

        forEachGroups.RemoveAll(group => string.Equals(group.GroupId, groupId, StringComparison.Ordinal));
        var group = new ForEachGroupRecord(groupId, workItems, maxConcurrency);
        forEachGroups.Add(group);
        UpdatedAt = recordedAt;
        LastActiveAt = recordedAt;
        return group;
    }

    internal void StartForEachWorkItem(ForEachGroupRecord group, int index, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.Start(index, startedAt);
        UpdatedAt = startedAt;
        LastActiveAt = startedAt;
    }

    internal void CompleteForEachWorkItem(ForEachGroupRecord group, int index, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.Complete(index, completedAt);
        UpdatedAt = completedAt;
        LastActiveAt = completedAt;
    }

    internal void FailForEachWorkItem(
        ForEachGroupRecord group,
        int index,
        string errorSummary,
        DateTimeOffset failedAt)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorSummary);

        group.Fail(index, errorSummary, failedAt);
        UpdatedAt = failedAt;
        LastActiveAt = failedAt;
    }

    internal IReadOnlyList<int> CancelForEachResidualWork(
        ForEachGroupRecord group,
        int completedIndex,
        DateTimeOffset cancelledAt)
    {
        ArgumentNullException.ThrowIfNull(group);

        var cancelled = group.CancelResiduals(completedIndex, cancelledAt);
        if (cancelled.Count > 0)
        {
            RecordLifecycleEvent("ForEachCancellationIntentRecorded", group.GroupId, Status, cancelledAt);
            UpdatedAt = cancelledAt;
            LastActiveAt = cancelledAt;
        }

        return cancelled;
    }

    internal void MarkStuckStep(string stepPath, DateTimeOffset detectedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepPath);
        lock (snapshotGate)
        {
            if (hasStuckStep && string.Equals(stuckStepPath, stepPath, StringComparison.Ordinal))
            {
                return;
            }

            hasStuckStep = true;
            isStuck = true;
            stuckStepPath = stepPath;
            stuckDetectedAt = detectedAt;
            RecordLifecycleEvent("StepStuckDetected", stepPath, Status, detectedAt);
            ToSnapshot();
        }
    }

    internal WorkflowInstanceSnapshot MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero || LifecycleMachine.TerminalStatuses.Contains(Status) || isStuck)
        {
            return ToSnapshot();
        }

        if (now - UpdatedAt <= threshold)
        {
            return ToSnapshot();
        }

        isStuck = true;
        stuckDetectedAt = now;
        RecordLifecycleEvent("InstanceStuckDetected", null, Status, now);
        return ToSnapshot();
    }

    internal void ResolveBranchRuntimeWork(BranchId branchId)
    {
        foreach (var wait in activeWaits.Where(wait => wait.BranchId == branchId).ToArray())
        {
            activeWaits.Remove(wait);
            wait.CancelLoser();
        }

        foreach (var timer in activeTimers.Where(timer => timer.BranchId == branchId).ToArray())
        {
            activeTimers.Remove(timer);
            timer.Cancel();
        }

        if (Status == WorkflowStatus.Waiting && activeWaits.Count == 0 && activeTimers.Count == 0)
        {
            Status = WorkflowStatus.Running;
        }
    }

    internal void RecordLifecycleEvent(
        string eventName,
        string? stepPath,
        WorkflowStatus? status,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        lock (snapshotGate)
        {
            if (lifecycleEvents.Count == maxLifecycleEvents)
            {
                lifecycleEvents.RemoveAt(0);
            }

            lifecycleEvents.Add(new LifecycleEventSnapshot
            {
                InstanceId = InstanceId,
                EventName = eventName,
                StepPath = stepPath,
                Status = status,
                OccurredAt = occurredAt,
                Durable = false
            });
        }
    }

    internal void StartStep(string stepPath, DateTimeOffset startedAt, TimeSpan? expectedTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepPath);

        lock (snapshotGate)
        {
            activeStep = new ActiveStepSnapshot
            {
                StepPath = stepPath,
                StartedAt = startedAt,
                ExpectedTimeout = expectedTimeout
            };
            UpdatedAt = startedAt;
            LastActiveAt = startedAt;
            PublishStepSnapshot();
        }
    }

    internal void CompleteStep(string stepPath, DateTimeOffset completedAt)
    {
        lock (snapshotGate)
        {
            if (activeStep is not null && string.Equals(activeStep.StepPath, stepPath, StringComparison.Ordinal))
            {
                activeStep = null;
            }

            UpdatedAt = completedAt;
            LastActiveAt = completedAt;
            PublishStepSnapshot();
        }
    }

    internal void Complete(string? outcomeName, DateTimeOffset updatedAt)
    {
        Status = WorkflowStatus.Completed;
        EndOutcomeName = outcomeName;
        UpdatedAt = updatedAt;
        CurrentStatusEnteredAt = updatedAt;
        LastActiveAt = updatedAt;
        RecordLifecycleEvent("InstanceCompleted", null, WorkflowStatus.Completed, updatedAt);
    }

    internal void Fail(WorkflowErrorDetails errorDetails)
    {
        if (Status is not WorkflowStatus.Running and not WorkflowStatus.Waiting)
        {
            throw new WorkflowLifecycleException(
                $"Lifecycle trigger '{LifecycleTrigger.Fail}' is not valid from workflow status '{Status}'.");
        }

        Status = WorkflowStatus.Failed;
        ErrorDetails = errorDetails;
        UpdatedAt = errorDetails.OccurredAt;
        CurrentStatusEnteredAt = errorDetails.OccurredAt;
        LastActiveAt = errorDetails.OccurredAt;
        RecordLifecycleEvent("InstanceFailed", null, WorkflowStatus.Failed, errorDetails.OccurredAt);
        ClearRuntimeWork();
    }

    internal void Compensate(DateTimeOffset updatedAt)
    {
        // Explicit operator compensation is allowed after a saga has completed.
        // The shared lifecycle machine models only forward-execution compensation,
        // so keep this ephemeral-only transition local to the saga runtime.
        if (Status == WorkflowStatus.Completed)
        {
            Status = WorkflowStatus.Compensated;
            UpdatedAt = updatedAt;
            CurrentStatusEnteredAt = updatedAt;
            LastActiveAt = updatedAt;
            RecordLifecycleEvent("InstanceCompensated", null, WorkflowStatus.Compensated, updatedAt);
            ClearRuntimeWork();
            return;
        }

        ApplyTerminalTrigger(LifecycleTrigger.Compensate, updatedAt);
    }

    internal void FailCompensation(WorkflowErrorDetails errorDetails)
    {
        FireOrThrow(LifecycleTrigger.FailCompensation);
        Status = WorkflowStatus.CompensationFailed;
        ErrorDetails = errorDetails;
        UpdatedAt = errorDetails.OccurredAt;
        CurrentStatusEnteredAt = errorDetails.OccurredAt;
        LastActiveAt = errorDetails.OccurredAt;
        RecordLifecycleEvent(
            "InstanceCompensationFailed",
            errorDetails.StepPath,
            WorkflowStatus.CompensationFailed,
            errorDetails.OccurredAt);
    }

    internal WorkflowInstanceSnapshot Cancel(DateTimeOffset updatedAt)
    {
        SignalCancellation();
        ApplyTerminalTrigger(LifecycleTrigger.Cancel, updatedAt);
        return ToSnapshot();
    }

    internal WorkflowInstanceSnapshot Terminate(DateTimeOffset updatedAt)
    {
        ApplyTerminalTrigger(LifecycleTrigger.Terminate, updatedAt);
        return ToSnapshot();
    }

    internal void ScheduleYield(Func<CancellationToken, Task> continueAsync)
    {
        ArgumentNullException.ThrowIfNull(continueAsync);

        if (LifecycleMachine.TerminalStatuses.Contains(Status))
        {
            throw new WorkflowLifecycleException(
                $"Cannot yield workflow instance '{InstanceId}' because status '{Status}' is terminal.");
        }

        yieldContinuations.Enqueue(continueAsync);
    }

    internal bool TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation)
    {
        if (yieldContinuations.Count == 0)
        {
            continuation = null;
            return false;
        }

        continuation = yieldContinuations.Dequeue();
        return true;
    }

    internal CancellationTokenSource CreateLinkedExecutionToken(CancellationToken cancellationToken)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(cancellationSource.Token, cancellationToken);
    }

    internal async ValueTask<IDisposable> EnterStateAccessAsync(CancellationToken cancellationToken)
    {
        await stateAccess.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new StateAccessLease(stateAccess);
    }

    internal void SignalCancellation()
    {
        if (!cancellationSource.IsCancellationRequested)
        {
            cancellationSource.Cancel();
        }
    }

    internal WorkflowInstanceSnapshot ToSnapshot()
    {
        lock (snapshotGate)
        {
            var snapshot = new WorkflowInstanceSnapshot
            {
                InstanceId = InstanceId,
                DefinitionId = DefinitionId,
                DefinitionVersion = DefinitionVersion,
                Status = Status,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                CurrentStatusEnteredAt = CurrentStatusEnteredAt,
                LastActiveAt = LastActiveAt,
                IsStuck = isStuck,
                HasStuckStep = hasStuckStep,
                StuckStepPath = stuckStepPath,
                StuckDetectedAt = stuckDetectedAt,
                ErrorSummary = ErrorDetails?.Summary,
                EndOutcomeName = EndOutcomeName,
                ActiveWaits = activeWaits.Select(wait => wait.ToSnapshot()).ToArray(),
                ActiveStep = activeStep,
                CompositionOutcomes = compositionOutcomes.ToArray(),
                ForEachGroups = forEachGroups.Select(group => group.ToSnapshot()).ToArray(),
                LifecycleEvents = lifecycleEvents.ToArray()
            };
            Volatile.Write(ref publishedSnapshot, snapshot);
            return snapshot;
        }
    }

    InstanceId IWorkflowInstance.InstanceId => InstanceId;

    Type IWorkflowInstance.StateType => typeof(TState);

    object IWorkflowInstance.StateObject => State!;

    object IWorkflowInstance.CopyState(IEphemeralStateSnapshotter snapshotter)
    {
        ArgumentNullException.ThrowIfNull(snapshotter);
        if (Volatile.Read(ref hasPublishedState))
        {
            lock (snapshotGate)
            {
                return snapshotter.Snapshot(publishedState!)!;
            }
        }

        stateAccess.Wait();
        try
        {
            return snapshotter.Snapshot(State)!;
        }
        finally
        {
            stateAccess.Release();
        }
    }

    bool IWorkflowInstance.HasPublishedState => Volatile.Read(ref hasPublishedState);

    void IWorkflowInstance.PublishState(IEphemeralStateSnapshotter snapshotter)
    {
        ArgumentNullException.ThrowIfNull(snapshotter);
        stateAccess.Wait();
        try
        {
            var snapshot = snapshotter.Snapshot(State);
            lock (snapshotGate)
            {
                publishedState = snapshot;
                Volatile.Write(ref hasPublishedState, true);
            }
        }
        finally
        {
            stateAccess.Release();
        }
    }

    WorkflowInstanceSnapshot IWorkflowInstance.ToSnapshot()
    {
        return ToSnapshot();
    }

    WorkflowInstanceSnapshot IWorkflowInstance.GetPublishedSnapshot()
    {
        return Volatile.Read(ref publishedSnapshot) ?? ToSnapshot();
    }

    WorkflowInstanceSnapshot IWorkflowInstance.Cancel(DateTimeOffset updatedAt)
    {
        return Cancel(updatedAt);
    }

    WorkflowInstanceSnapshot IWorkflowInstance.Terminate(DateTimeOffset updatedAt)
    {
        return Terminate(updatedAt);
    }

    WorkflowInstanceSnapshot IWorkflowInstance.MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold)
    {
        return MarkStuckIfNoProgress(now, threshold);
    }

    bool IWorkflowInstance.TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation)
    {
        return TryTakeYieldContinuation(out continuation);
    }

    CancellationTokenSource IWorkflowInstance.CreateLinkedExecutionToken(CancellationToken cancellationToken)
    {
        return CreateLinkedExecutionToken(cancellationToken);
    }

    void IWorkflowInstance.SignalCancellation()
    {
        SignalCancellation();
    }

    private void ApplyTerminalTrigger(LifecycleTrigger trigger, DateTimeOffset updatedAt)
    {
        var result = LifecycleMachine.Fire(Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }

        Status = result.Value;
        UpdatedAt = updatedAt;
        CurrentStatusEnteredAt = updatedAt;
        LastActiveAt = updatedAt;
        RecordLifecycleEvent($"Instance{Status}", null, Status, updatedAt);
        ClearRuntimeWork();
    }

    private void ClearRuntimeWork()
    {
        foreach (var wait in activeWaits)
        {
            wait.CancelLoser();
        }

        foreach (var timer in activeTimers)
        {
            timer.Cancel();
        }

        activeWaits.Clear();
        activeTimers.Clear();
        pendingEvents.Clear();
        yieldContinuations.Clear();
        activeStep = null;
    }

    private void FireOrThrow(LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }
    }

    private bool HasSeen(EventId eventId)
    {
        return consumedEventIds.Contains(eventId) || pendingEvents.Any(envelope => envelope.EventId == eventId);
    }

    private void RecordConsumedEvent(EventId eventId)
    {
        if (!consumedEventIds.Add(eventId))
        {
            return;
        }

        consumedEventOrder.Enqueue(eventId);
        if (consumedEventOrder.Count > maxConsumedEventIds)
        {
            consumedEventIds.Remove(consumedEventOrder.Dequeue());
        }
    }

    private void PublishStepSnapshot()
    {
        var current = Volatile.Read(ref publishedSnapshot);
        if (current is null)
        {
            ToSnapshot();
            return;
        }

        Volatile.Write(ref publishedSnapshot, current with
        {
            UpdatedAt = UpdatedAt,
            LastActiveAt = LastActiveAt,
            ActiveStep = activeStep
        });
    }

    private sealed class StateAccessLease(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose()
        {
            semaphore.Release();
        }
    }

    private bool HasConsumedWait(EventEnvelope envelope)
    {
        var signature = WaitSignature.From(envelope);
        return consumedWaits.Contains(signature) ||
            (string.IsNullOrWhiteSpace(envelope.BranchId) && consumedWaits.Any(wait =>
                string.Equals(wait.EventName, envelope.EventName, StringComparison.Ordinal) &&
                wait.CorrelationId == envelope.CorrelationId &&
                wait.BranchId is null));
    }

    private bool HasTimedOutWait(EventEnvelope envelope)
    {
        var signature = WaitSignature.From(envelope);
        return IsStaleForTimedOutWait(signature, envelope.OccurredAt) ||
            (string.IsNullOrWhiteSpace(envelope.BranchId) && timedOutWaits.Any(wait =>
                string.Equals(wait.Key.EventName, envelope.EventName, StringComparison.Ordinal) &&
                wait.Key.CorrelationId == envelope.CorrelationId &&
                wait.Key.BranchId is null &&
                envelope.OccurredAt < wait.Value));
    }

    private bool IsStaleForTimedOutWait(WaitSignature signature, DateTimeOffset occurredAt)
    {
        return timedOutWaits.TryGetValue(signature, out var timedOutAt) && occurredAt < timedOutAt;
    }

    private readonly record struct WaitSignature(string EventName, CorrelationId CorrelationId, BranchId? BranchId)
    {
        internal static WaitSignature From(EventEnvelope envelope)
        {
            BranchId? branchId = string.IsNullOrWhiteSpace(envelope.BranchId)
                ? null
                : ParseBranchId(envelope.BranchId);
            return new WaitSignature(envelope.EventName, envelope.CorrelationId, branchId);
        }

        private static BranchId ParseBranchId(string value)
        {
            var separator = value.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator == value.Length - 1)
            {
                throw new WorkflowRoutingException($"BranchId '{value}' is not in '<ordinal>:<name>' format.");
            }

            if (!int.TryParse(value[..separator], out var ordinal))
            {
                throw new WorkflowRoutingException($"BranchId '{value}' does not start with a numeric ordinal.");
            }

            return new BranchId(ordinal, value[(separator + 1)..]);
        }
    }

    internal sealed class ForEachGroupRecord
    {
        private readonly List<ForEachWorkItemRecord> workItems;

        internal ForEachGroupRecord(
            string groupId,
            IReadOnlyList<ForEachWorkItemSnapshot> workItems,
            int? maxConcurrency)
        {
            GroupId = groupId;
            MaxConcurrency = maxConcurrency;
            this.workItems = workItems
                .Select(workItem => new ForEachWorkItemRecord(workItem))
                .ToList();
        }

        internal string GroupId { get; }

        internal int? MaxConcurrency { get; }

        internal int Count => workItems.Count;

        internal int ActiveCount => workItems.Count(workItem => workItem.Status == ForEachWorkItemStatus.Active);

        internal int CompletedCount => workItems.Count(workItem => workItem.Status == ForEachWorkItemStatus.Completed);

        internal int FailedCount => workItems.Count(workItem => workItem.Status == ForEachWorkItemStatus.Failed);

        internal int CancelledCount => workItems.Count(workItem => workItem.Status == ForEachWorkItemStatus.Cancelled);

        internal void Start(int index, DateTimeOffset startedAt)
        {
            var workItem = Get(index);
            workItem.Status = ForEachWorkItemStatus.Active;
            workItem.StartedAt ??= startedAt;
        }

        internal void Complete(int index, DateTimeOffset completedAt)
        {
            var workItem = Get(index);
            workItem.Status = ForEachWorkItemStatus.Completed;
            workItem.CompletedAt = completedAt;
        }

        internal void Fail(int index, string errorSummary, DateTimeOffset failedAt)
        {
            var workItem = Get(index);
            workItem.Status = ForEachWorkItemStatus.Failed;
            workItem.CompletedAt = failedAt;
            workItem.ErrorSummary = errorSummary;
        }

        internal IReadOnlyList<int> CancelResiduals(int completedIndex, DateTimeOffset cancelledAt)
        {
            var cancelled = new List<int>();
            foreach (var workItem in workItems.Where(workItem =>
                         workItem.Index != completedIndex &&
                         workItem.Status is not ForEachWorkItemStatus.Completed and not ForEachWorkItemStatus.Failed))
            {
                workItem.Status = ForEachWorkItemStatus.Cancelled;
                workItem.CompletedAt = cancelledAt;
                cancelled.Add(workItem.Index);
            }

            return cancelled;
        }

        internal ForEachGroupSnapshot ToSnapshot()
        {
            return new ForEachGroupSnapshot
            {
                GroupId = GroupId,
                TotalItems = workItems.Sum(workItem => workItem.Items.Count),
                WorkItemCount = workItems.Count,
                MaxConcurrency = MaxConcurrency,
                ActiveCount = ActiveCount,
                CompletedCount = CompletedCount,
                FailedCount = FailedCount,
                CancelledCount = CancelledCount,
                WorkItems = workItems.Select(workItem => workItem.ToSnapshot()).ToArray()
            };
        }

        private ForEachWorkItemRecord Get(int index)
        {
            var workItem = workItems.SingleOrDefault(candidate => candidate.Index == index);
            return workItem ?? throw new InvalidOperationException(
                $"ForEach group '{GroupId}' does not contain work item '{index}'.");
        }
    }

    private sealed class ForEachWorkItemRecord
    {
        internal ForEachWorkItemRecord(ForEachWorkItemSnapshot snapshot)
        {
            Index = snapshot.Index;
            Items = snapshot.Items.ToArray();
            Status = snapshot.Status;
            StartedAt = snapshot.StartedAt;
            CompletedAt = snapshot.CompletedAt;
            ErrorSummary = snapshot.ErrorSummary;
        }

        internal int Index { get; }

        internal IReadOnlyList<object?> Items { get; }

        internal ForEachWorkItemStatus Status { get; set; }

        internal DateTimeOffset? StartedAt { get; set; }

        internal DateTimeOffset? CompletedAt { get; set; }

        internal string? ErrorSummary { get; set; }

        internal ForEachWorkItemSnapshot ToSnapshot()
        {
            return new ForEachWorkItemSnapshot
            {
                Index = Index,
                Items = Items,
                Status = Status,
                StartedAt = StartedAt,
                CompletedAt = CompletedAt,
                ErrorSummary = ErrorSummary
            };
        }
    }
}
