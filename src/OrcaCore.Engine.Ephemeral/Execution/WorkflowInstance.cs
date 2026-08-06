using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowInstance<TState> : IWorkflowInstance
{
    private readonly List<RuntimeWaitRecord> activeWaits = [];
    private readonly HashSet<EventId> consumedEventIds = [];
    private readonly Queue<EventId> consumedEventOrder = [];
    private readonly HashSet<WaitSignature> consumedWaits = [];
    private readonly Dictionary<WaitSignature, DateTimeOffset> timedOutWaits = [];
    private readonly List<EphemeralCompositionBranchOutcomeSnapshot> compositionOutcomes = [];
    private readonly List<ForEachGroupRecord> forEachGroups = [];
    private readonly List<EphemeralLifecycleEventSnapshot> lifecycleEvents = [];
    private readonly List<EventEnvelope> pendingEvents = [];
    private readonly List<RuntimeTimerRecord> activeTimers = [];
    private readonly Dictionary<string, long> stepOccurrences = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource cancellationSource = new();
    private readonly object snapshotGate = new();
    private readonly SemaphoreSlim stateAccess = new(1, 1);
    private readonly int maxConsumedEventIds;
    private readonly int maxLifecycleEvents;
    private readonly int maxPendingEvents;
    private EphemeralActiveStepSnapshot? activeStep;
    private readonly Queue<Func<CancellationToken, Task>> yieldContinuations = [];
    private bool hasStuckStep;
    private bool isStuck;
    private DateTimeOffset currentStatusEnteredAt;
    private DateTimeOffset lastActiveAt;
    private DateTimeOffset? stuckDetectedAt;
    private string? stuckStepPath;
    private long nextWaitSequence;
    private EphemeralWorkflowInstanceSnapshot? publishedSnapshot;
    private TState? publishedState;
    private bool hasPublishedState;
    private int cancellationRequested;

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
        Status = global::OrcaCore.WorkflowInstanceStatus.Running;
        RecordLifecycleEvent("InstanceStarted", null, global::OrcaCore.WorkflowInstanceStatus.Running, createdAt);
        ToSnapshot();
    }

    internal InstanceId InstanceId { get; }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    internal TState State { get; private set; }

    internal global::OrcaCore.WorkflowInstanceStatus Status { get; private set; }

    internal void ApplyStructuredStatus(global::OrcaCore.WorkflowInstanceStatus status, DateTimeOffset observedAt)
    {
        if (status is not (global::OrcaCore.WorkflowInstanceStatus.Running or global::OrcaCore.WorkflowInstanceStatus.Waiting))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Structured runtime status updates must be nonterminal.");
        }

        if (Status == status)
        {
            return;
        }

        Status = status;
        UpdatedAt = observedAt;
        CurrentStatusEnteredAt = observedAt;
        ToSnapshot();
    }

    internal void ReplaceState(TState state)
    {
        State = state;
    }

    internal StepOperationId BeginStepOperation(string stepPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepPath);
        var occurrence = stepOccurrences.TryGetValue(stepPath, out var current)
            ? checked(current + 1)
            : 1;
        stepOccurrences[stepPath] = occurrence;
        return StepOperationId.Parse($"{InstanceId}:{stepPath}:{occurrence:D8}");
    }

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

    internal StructuredSerializedValue? Output { get; private set; }

    internal Type? OutputType => Output?.DeclaredType;

    internal byte[]? OutputPayload => Output?.Payload.ToArray();

    Type? IWorkflowInstance.OutputType => OutputType;

    byte[]? IWorkflowInstance.CopyOutputPayload() => OutputPayload;

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
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        BranchId? branchId,
        DateTimeOffset registeredAt,
        string authoredPath,
        DateTimeOffset? deadline,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync,
        FiberId? fiberId = null,
        ScopeId? scopeId = null)
    {
        Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
        UpdatedAt = registeredAt;
        CurrentStatusEnteredAt = registeredAt;
        var wait = new RuntimeWaitRecord(
            eventContract,
            correlationId,
            branchId,
            registeredAt,
            authoredPath,
            deadline,
            resumeAsync,
            checked(++nextWaitSequence),
            fiberId,
            scopeId);
        activeWaits.Add(wait);
        return wait;
    }

    internal void CancelWait(RuntimeWaitRecord wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        if (activeWaits.Remove(wait))
        {
            wait.CancelLoser();
        }

        RestoreRunningWhenRuntimeWorkIsClear();
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> RaiseEventAsync(
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

        var wait = activeWaits
            .Where(candidate => candidate.Matches(envelope))
            .OrderBy(candidate => candidate.WaitSequence)
            .ThenBy(candidate => candidate.FiberId?.Value ?? string.Empty, StringComparer.Ordinal)
            .FirstOrDefault();
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

    internal async Task<EphemeralWorkflowInstanceSnapshot> MatchPendingEventAsync(
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
        Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
        UpdatedAt = registeredAt;
        CurrentStatusEnteredAt = registeredAt;
        var timer = new RuntimeTimerRecord(branchId, registeredAt);
        activeTimers.Add(timer);
        return timer;
    }

    internal void CancelTimer(RuntimeTimerRecord timer)
    {
        ArgumentNullException.ThrowIfNull(timer);
        if (activeTimers.Remove(timer))
        {
            timer.Cancel();
        }

        RestoreRunningWhenRuntimeWorkIsClear();
    }

    private void RestoreRunningWhenRuntimeWorkIsClear()
    {
        if (Status == global::OrcaCore.WorkflowInstanceStatus.Waiting && activeWaits.Count == 0 && activeTimers.Count == 0)
        {
            Status = global::OrcaCore.WorkflowInstanceStatus.Running;
        }
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> FireDelayAsync(
        RuntimeTimerRecord timer,
        DateTimeOffset firedAt,
        Func<CancellationToken, Task> resumeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(resumeAsync);

        var removed = activeTimers.Remove(timer);
        if (!removed)
        {
            return ToSnapshot();
        }

        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = global::OrcaCore.WorkflowInstanceStatus.Running;
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

            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
            UpdatedAt = timer.RegisteredAt;
            CurrentStatusEnteredAt = timer.RegisteredAt;
            throw;
        }

        if (Status == global::OrcaCore.WorkflowInstanceStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
            CurrentStatusEnteredAt = firedAt;
        }

        return ToSnapshot();
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> FireWaitTimeoutAsync(
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
        var signature = new WaitSignature(wait.EventContract, wait.CorrelationId, wait.BranchId);
        var consumedWaitAdded = consumedWaits.Add(signature);
        var hadPreviousTimeout = timedOutWaits.TryGetValue(signature, out var previousTimeout);
        timedOutWaits[signature] = firedAt;
        var removedPendingEvents = pendingEvents
            .Where(signature.Matches)
            .ToArray();
        pendingEvents.RemoveAll(candidate => signature.Matches(candidate));
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = global::OrcaCore.WorkflowInstanceStatus.Running;
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
                if (pendingEvents.All(candidate => !candidate.EventId.Equals(pendingEvent.EventId)))
                {
                    pendingEvents.Add(pendingEvent);
                }
            }

            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
            UpdatedAt = wait.RegisteredAt;
            CurrentStatusEnteredAt = wait.RegisteredAt;
            throw;
        }

        if (Status == global::OrcaCore.WorkflowInstanceStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
            CurrentStatusEnteredAt = firedAt;
        }

        return ToSnapshot();
    }

    private async Task<EphemeralWorkflowInstanceSnapshot> ResumeWaitAsync(
        RuntimeWaitRecord wait,
        EventEnvelope envelope,
        DateTimeOffset processedAt,
        bool removePendingAfterCommit,
        CancellationToken cancellationToken)
    {
        wait.MarkMatched();
        activeWaits.Remove(wait);
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = global::OrcaCore.WorkflowInstanceStatus.Running;
        UpdatedAt = processedAt;
        CurrentStatusEnteredAt = processedAt;
        LastActiveAt = processedAt;

        var removedPendingEvent = removePendingAfterCommit &&
            pendingEvents.RemoveAll(candidate => candidate.EventId.Equals(envelope.EventId)) > 0;

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

            if (removedPendingEvent && pendingEvents.All(candidate => !candidate.EventId.Equals(envelope.EventId)))
            {
                pendingEvents.Add(envelope);
            }

            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
            UpdatedAt = wait.RegisteredAt;
            CurrentStatusEnteredAt = wait.RegisteredAt;
            throw;
        }

        wait.CancelLoser();
        RecordConsumedEvent(envelope.EventId);
        consumedWaits.Add(new WaitSignature(wait.EventContract, wait.CorrelationId, wait.BranchId));
        if (Status == global::OrcaCore.WorkflowInstanceStatus.Running && (activeWaits.Count > 0 || activeTimers.Count > 0))
        {
            Status = global::OrcaCore.WorkflowInstanceStatus.Waiting;
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
        compositionOutcomes.Add(new EphemeralCompositionBranchOutcomeSnapshot
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
        IReadOnlyList<EphemeralForEachWorkItemSnapshot> workItems,
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

    internal EphemeralWorkflowInstanceSnapshot MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold)
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

        if (Status == global::OrcaCore.WorkflowInstanceStatus.Waiting && activeWaits.Count == 0 && activeTimers.Count == 0)
        {
            Status = global::OrcaCore.WorkflowInstanceStatus.Running;
        }
    }

    internal void RecordLifecycleEvent(
        string eventName,
        string? stepPath,
        global::OrcaCore.WorkflowInstanceStatus? status,
        DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        lock (snapshotGate)
        {
            if (lifecycleEvents.Count == maxLifecycleEvents)
            {
                lifecycleEvents.RemoveAt(0);
            }

            lifecycleEvents.Add(new EphemeralLifecycleEventSnapshot
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
            activeStep = new EphemeralActiveStepSnapshot
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

    internal void Complete(
        string? outcomeName,
        DateTimeOffset updatedAt,
        StructuredSerializedValue? output = null)
    {
        Status = global::OrcaCore.WorkflowInstanceStatus.Completed;
        EndOutcomeName = outcomeName;
        Output = output;
        UpdatedAt = updatedAt;
        CurrentStatusEnteredAt = updatedAt;
        LastActiveAt = updatedAt;
        RecordLifecycleEvent("InstanceCompleted", null, global::OrcaCore.WorkflowInstanceStatus.Completed, updatedAt);
    }

    internal EphemeralWorkflowInstanceSnapshot Timeout(
        DateTimeOffset deadline,
        DateTimeOffset observedAt)
    {
        if (LifecycleMachine.TerminalStatuses.Contains(Status))
        {
            return ToSnapshot();
        }

        SignalCancellation();
        ApplyTerminalTrigger(LifecycleTrigger.Timeout, observedAt);
        var exception = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.WorkflowDeadline(deadline);
        ErrorDetails = new WorkflowErrorDetails(
            exception.Code,
            exception.Message,
            "workflow:$",
            observedAt);
        return ToSnapshot();
    }

    internal void Fail(WorkflowErrorDetails errorDetails)
    {
        if (Status is not global::OrcaCore.WorkflowInstanceStatus.Running and not global::OrcaCore.WorkflowInstanceStatus.Waiting)
        {
            throw new WorkflowLifecycleException(
                $"Lifecycle trigger '{LifecycleTrigger.Fail}' is not valid from workflow status '{Status}'.");
        }

        Status = global::OrcaCore.WorkflowInstanceStatus.Failed;
        ErrorDetails = errorDetails;
        UpdatedAt = errorDetails.OccurredAt;
        CurrentStatusEnteredAt = errorDetails.OccurredAt;
        LastActiveAt = errorDetails.OccurredAt;
        RecordLifecycleEvent("InstanceFailed", null, global::OrcaCore.WorkflowInstanceStatus.Failed, errorDetails.OccurredAt);
        ClearRuntimeWork();
    }

    internal EphemeralWorkflowInstanceSnapshot Cancel(DateTimeOffset updatedAt)
    {
        SignalCancellation();
        ApplyTerminalTrigger(LifecycleTrigger.Cancel, updatedAt);
        return ToSnapshot();
    }

    internal EphemeralWorkflowInstanceSnapshot Terminate(DateTimeOffset updatedAt)
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

    internal bool IsCancellationRequested => Volatile.Read(ref cancellationRequested) != 0;

    internal global::OrcaCore.WorkflowCancellationRequestStatus TryRequestCancellation(
        DateTimeOffset requestedAt)
    {
        lock (snapshotGate)
        {
            if (LifecycleMachine.TerminalStatuses.Contains(Status))
            {
                return global::OrcaCore.WorkflowCancellationRequestStatus.AlreadyTerminal;
            }

            if (Interlocked.CompareExchange(ref cancellationRequested, 1, 0) != 0)
            {
                return global::OrcaCore.WorkflowCancellationRequestStatus.AlreadyRequested;
            }

            UpdatedAt = requestedAt;
            CurrentStatusEnteredAt = requestedAt;
            ToSnapshot();
        }

        SignalCancellation();
        return global::OrcaCore.WorkflowCancellationRequestStatus.Requested;
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

    internal EphemeralWorkflowInstanceSnapshot ToSnapshot()
    {
        lock (snapshotGate)
        {
            var snapshot = new EphemeralWorkflowInstanceSnapshot
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

    object IWorkflowInstance.CopyState()
    {
        if (Volatile.Read(ref hasPublishedState))
        {
            lock (snapshotGate)
            {
                return CopyStateWithFixedCodec(publishedState!)!;
            }
        }

        stateAccess.Wait();
        try
        {
            return CopyStateWithFixedCodec(State)!;
        }
        finally
        {
            stateAccess.Release();
        }
    }

    bool IWorkflowInstance.HasPublishedState => Volatile.Read(ref hasPublishedState);

    void IWorkflowInstance.PublishState()
    {
        stateAccess.Wait();
        try
        {
            var snapshot = CopyStateWithFixedCodec(State);
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

    private static TState CopyStateWithFixedCodec(TState state)
    {
        var payload = CoreWorkflowValueCodec.Serialize(state, typeof(TState));
        return (TState)CoreWorkflowValueCodec.Deserialize(payload, typeof(TState))!;
    }

    EphemeralWorkflowInstanceSnapshot IWorkflowInstance.ToSnapshot()
    {
        return ToSnapshot();
    }

    EphemeralWorkflowInstanceSnapshot IWorkflowInstance.GetPublishedSnapshot()
    {
        return Volatile.Read(ref publishedSnapshot) ?? ToSnapshot();
    }

    EphemeralWorkflowInstanceSnapshot IWorkflowInstance.Cancel(DateTimeOffset updatedAt)
    {
        return Cancel(updatedAt);
    }

    EphemeralWorkflowInstanceSnapshot IWorkflowInstance.Terminate(DateTimeOffset updatedAt)
    {
        return Terminate(updatedAt);
    }

    EphemeralWorkflowInstanceSnapshot IWorkflowInstance.MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold)
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

    bool IWorkflowInstance.IsCancellationRequested => IsCancellationRequested;

    global::OrcaCore.WorkflowCancellationRequestStatus IWorkflowInstance.TryRequestCancellation(
        DateTimeOffset requestedAt)
    {
        return TryRequestCancellation(requestedAt);
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
        return consumedEventIds.Contains(eventId) || pendingEvents.Any(envelope => envelope.EventId.Equals(eventId));
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
        return consumedWaits.Any(wait => wait.Matches(envelope));
    }

    private bool HasTimedOutWait(EventEnvelope envelope)
    {
        return timedOutWaits.Any(wait =>
            wait.Key.Matches(envelope) && envelope.OccurredAt < wait.Value);
    }

    private readonly record struct WaitSignature(
        WorkflowEventContract EventContract,
        CorrelationId CorrelationId,
        BranchId? BranchId)
    {
        internal bool Matches(EventEnvelope envelope) =>
            EventContract.Equals(envelope.EventContract) &&
            CorrelationId.Equals(envelope.CorrelationId);
    }

    internal sealed class ForEachGroupRecord
    {
        private readonly List<ForEachWorkItemRecord> workItems;

        internal ForEachGroupRecord(
            string groupId,
            IReadOnlyList<EphemeralForEachWorkItemSnapshot> workItems,
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

        internal int ActiveCount => workItems.Count(workItem => workItem.Status == EphemeralForEachWorkItemStatus.Active);

        internal int CompletedCount => workItems.Count(workItem => workItem.Status == EphemeralForEachWorkItemStatus.Completed);

        internal int FailedCount => workItems.Count(workItem => workItem.Status == EphemeralForEachWorkItemStatus.Failed);

        internal int CancelledCount => workItems.Count(workItem => workItem.Status == EphemeralForEachWorkItemStatus.Cancelled);

        internal void Start(int index, DateTimeOffset startedAt)
        {
            var workItem = Get(index);
            workItem.Status = EphemeralForEachWorkItemStatus.Active;
            workItem.StartedAt ??= startedAt;
        }

        internal void Complete(int index, DateTimeOffset completedAt)
        {
            var workItem = Get(index);
            workItem.Status = EphemeralForEachWorkItemStatus.Completed;
            workItem.CompletedAt = completedAt;
        }

        internal void Fail(int index, string errorSummary, DateTimeOffset failedAt)
        {
            var workItem = Get(index);
            workItem.Status = EphemeralForEachWorkItemStatus.Failed;
            workItem.CompletedAt = failedAt;
            workItem.ErrorSummary = errorSummary;
        }

        internal IReadOnlyList<int> CancelResiduals(int completedIndex, DateTimeOffset cancelledAt)
        {
            var cancelled = new List<int>();
            foreach (var workItem in workItems.Where(workItem =>
                         workItem.Index != completedIndex &&
                         workItem.Status is not EphemeralForEachWorkItemStatus.Completed and not EphemeralForEachWorkItemStatus.Failed))
            {
                workItem.Status = EphemeralForEachWorkItemStatus.Cancelled;
                workItem.CompletedAt = cancelledAt;
                cancelled.Add(workItem.Index);
            }

            return cancelled;
        }

        internal EphemeralForEachGroupSnapshot ToSnapshot()
        {
            return new EphemeralForEachGroupSnapshot
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
        internal ForEachWorkItemRecord(EphemeralForEachWorkItemSnapshot snapshot)
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

        internal EphemeralForEachWorkItemStatus Status { get; set; }

        internal DateTimeOffset? StartedAt { get; set; }

        internal DateTimeOffset? CompletedAt { get; set; }

        internal string? ErrorSummary { get; set; }

        internal EphemeralForEachWorkItemSnapshot ToSnapshot()
        {
            return new EphemeralForEachWorkItemSnapshot
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
