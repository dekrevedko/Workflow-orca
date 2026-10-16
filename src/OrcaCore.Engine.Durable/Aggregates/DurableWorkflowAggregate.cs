using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWorkflowAggregate
{
    private readonly DurableResourcePoolState resourcePoolState;
    private readonly DurableTimerState timerState;
    private readonly DurableWaitState waitState;

    private DurableWorkflowAggregate(DurableAggregateState state)
    {
        InstanceId = state.InstanceId;
        StreamVersion = state.StreamVersion;
        ParentInstanceId = state.ParentInstanceId;
        RootInstanceId = state.RootInstanceId;
        DefinitionId = state.DefinitionId;
        DefinitionVersion = state.DefinitionVersion;
        Status = state.Status;
        CreatedAt = state.CreatedAt;
        UpdatedAt = state.UpdatedAt;
        LastStepPath = state.LastStepPath;
        ErrorSummary = state.ErrorSummary;
        OutcomeName = state.OutcomeName;
        ContinueAsNewGeneration = state.ContinueAsNewGeneration;
        StartInputContentType = state.StartInputContentType;
        StartInputPayload = state.StartInputPayload;
        ParkReason = state.ParkReason;
        ContinuationFailureCount = state.ContinuationFailureCount;
        ContinuationFailurePositionStreamVersion = state.ContinuationFailurePositionStreamVersion;
        ContinuationRetryNotBefore = state.ContinuationRetryNotBefore;
        timerState = DurableTimerState.FromSnapshot(state.ActiveTimers);
        waitState = DurableWaitState.FromSnapshot(state.ActiveWaits, state.PendingResumes);
        resourcePoolState = DurableResourcePoolState.FromSnapshot(state.ActiveResourceTickets);
    }

    internal InstanceId InstanceId { get; set; }

    internal StreamVersion StreamVersion { get; set; }

    internal InstanceId? ParentInstanceId { get; set; }

    internal InstanceId? RootInstanceId { get; set; }

    internal DefinitionId? DefinitionId { get; set; }

    internal DefinitionVersion? DefinitionVersion { get; set; }

    internal WorkflowStatus? Status { get; set; }

    internal DateTimeOffset? CreatedAt { get; set; }

    internal DateTimeOffset? UpdatedAt { get; set; }

    internal string? LastStepPath { get; set; }

    internal string? ErrorSummary { get; set; }

    internal string? OutcomeName { get; set; }

    internal int ContinueAsNewGeneration { get; set; }

    /// <summary>
    /// Gets or sets the serialized start input content type recorded by the start fact.
    /// </summary>
    internal string? StartInputContentType { get; set; }

    /// <summary>
    /// Gets or sets the serialized start input recorded by the start fact.
    /// </summary>
    internal byte[]? StartInputPayload { get; set; }

    /// <summary>
    /// Gets or sets the park reason while the instance status is Parked.
    /// </summary>
    internal DurableParkReason? ParkReason { get; set; }

    internal int ContinuationFailureCount { get; set; }

    internal StreamVersion? ContinuationFailurePositionStreamVersion { get; set; }

    internal DateTimeOffset? ContinuationRetryNotBefore { get; set; }

    internal DurableResourcePoolState ResourcePoolState => resourcePoolState;

    internal DurableTimerState TimerState => timerState;

    internal DurableWaitState WaitState => waitState;

    internal DurableAggregateSnapshot Snapshot => new(
        InstanceId,
        DefinitionId,
        DefinitionVersion,
        ParentInstanceId,
        RootInstanceId,
        Status,
        CreatedAt,
        UpdatedAt,
        LastStepPath,
        ErrorSummary,
        OutcomeName,
        ContinueAsNewGeneration,
        timerState.ActiveTimers,
        waitState.ActiveWaits,
        resourcePoolState.ActiveTickets)
    {
        PendingResumes = waitState.PendingResumes,
        StartInputContentType = StartInputContentType,
        StartInputPayload = StartInputPayload,
        ParkReason = ParkReason
    };

    internal bool IsTerminal =>
        Status is WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.TimedOut;

    internal static DurableWorkflowAggregate Empty(InstanceId instanceId)
    {
        return new DurableWorkflowAggregate(new DurableAggregateState { InstanceId = instanceId });
    }

    internal static DurableWorkflowAggregate Rehydrate(
        DurableAggregateCheckpoint? checkpoint,
        IEnumerable<DurableWorkflowEvent> tail)
    {
        ArgumentNullException.ThrowIfNull(tail);
        var tailEvents = tail as IReadOnlyList<DurableWorkflowEvent> ?? tail.ToArray();
        var instanceId = checkpoint?.InstanceId ?? tailEvents.FirstOrDefault()?.InstanceId ??
            throw new ArgumentException("A checkpoint or at least one tail event is required.", nameof(tail));

        return Rehydrate(instanceId, checkpoint, tailEvents);
    }

    internal static DurableWorkflowAggregate Rehydrate(
        InstanceId instanceId,
        DurableAggregateCheckpoint? checkpoint,
        IEnumerable<DurableWorkflowEvent> tail)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(tail);

        var aggregate = checkpoint is null
            ? Empty(instanceId)
            : new DurableWorkflowAggregate(ToState(checkpoint));

        foreach (var workflowEvent in tail)
        {
            aggregate.Apply(workflowEvent);
        }

        return aggregate;
    }

    /// <summary>
    /// Maps a persisted checkpoint onto the construction memento. Start input and park reason
    /// are intentionally absent: checkpoints do not carry them; only replay restores them.
    /// </summary>
    private static DurableAggregateState ToState(DurableAggregateCheckpoint checkpoint)
    {
        return new DurableAggregateState
        {
            InstanceId = checkpoint.InstanceId,
            StreamVersion = checkpoint.StreamVersion,
            ParentInstanceId = checkpoint.ParentInstanceId,
            RootInstanceId = checkpoint.RootInstanceId,
            DefinitionId = checkpoint.DefinitionId,
            DefinitionVersion = checkpoint.DefinitionVersion,
            Status = checkpoint.Status,
            CreatedAt = checkpoint.CreatedAt,
            UpdatedAt = checkpoint.UpdatedAt,
            LastStepPath = checkpoint.LastStepPath,
            ErrorSummary = checkpoint.ErrorSummary,
            OutcomeName = checkpoint.OutcomeName,
            ContinueAsNewGeneration = checkpoint.ContinueAsNewGeneration,
            ActiveTimers = checkpoint.ActiveTimers,
            ActiveWaits = checkpoint.ActiveWaits,
            ActiveResourceTickets = checkpoint.ActiveResourceTickets,
            PendingResumes = checkpoint.PendingResumes,
            ContinuationFailureCount = checkpoint.ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = checkpoint.ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = checkpoint.ContinuationRetryNotBefore
        };
    }

    /// <summary>
    /// Captures the aggregate's current state as a construction memento (including replay-only
    /// facts: start input and park reason), so a detached copy is field-complete.
    /// </summary>
    private DurableAggregateState CaptureState()
    {
        return new DurableAggregateState
        {
            InstanceId = InstanceId,
            StreamVersion = StreamVersion,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId,
            DefinitionId = DefinitionId,
            DefinitionVersion = DefinitionVersion,
            Status = Status,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            LastStepPath = LastStepPath,
            ErrorSummary = ErrorSummary,
            OutcomeName = OutcomeName,
            ContinueAsNewGeneration = ContinueAsNewGeneration,
            ActiveTimers = timerState.ActiveTimers,
            ActiveWaits = waitState.ActiveWaits,
            ActiveResourceTickets = resourcePoolState.ActiveTickets,
            PendingResumes = waitState.PendingResumes,
            StartInputContentType = StartInputContentType,
            StartInputPayload = StartInputPayload,
            ParkReason = ParkReason,
            ContinuationFailureCount = ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = ContinuationRetryNotBefore
        };
    }

    internal DurableAggregateCheckpoint CreateCheckpoint(string contentType, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(payload);

        return new DurableAggregateCheckpoint(
            InstanceId,
            StreamVersion,
            ParentInstanceId,
            RootInstanceId,
            DefinitionId,
            DefinitionVersion,
            Status,
            CreatedAt,
            UpdatedAt,
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            ContinueAsNewGeneration,
            timerState.ActiveTimers,
            waitState.ActiveWaits,
            resourcePoolState.ActiveTickets,
            contentType,
            [.. payload])
        {
            PendingResumes = waitState.PendingResumes
        };
    }

    internal DurableDecision DecideStart(StartWorkflowCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideStepCompleted(DurableStepCompletedCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideContinueAsNew(ContinueAsNewCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideStepFailed(DurableStepFailedCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideFiberFailed(DurableFiberFailedCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideYield(DurableYieldCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideWaitRegistered(DurableWaitRegisteredCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideCancel(CancelWorkflowCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideWaitMatched(DurableWaitMatchedCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideTimerScheduled(ScheduleTimerCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideTimerFired(FireTimerCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideDeliverEvent(DeliverEventCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideComplete(DurableCompleteCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideFail(DurableFailCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideTimeout(DurableTimeoutCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecidePark(DurableParkCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideUnpark(DurableUnparkCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideContinuationAttemptFailed(DurableContinuationAttemptFailedCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideContinuationAttemptReset(DurableContinuationAttemptResetCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideTerminate(TerminateWorkflowCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideTerminalLifecycle(DurableTerminalLifecycleCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideResourcePoolAcquire(
        AcquireResourcePoolCommand command,
        ResourcePoolAcquireResult acquireResult) =>
        DurableResourcePoolCommandHandler.Handle(this, command, acquireResult);

    internal DurableDecision DecideLeaseStopConfirmed(
        DurableLeaseStopConfirmedCommand command) =>
        DurableResourcePoolCommandHandler.Handle(this, command);

    /// <summary>
    /// Creates a detached copy of this aggregate and applies the supplied uncommitted events to
    /// it, so decisions can materialize post-commit state (checkpoints, projections) without
    /// mutating the decision-time aggregate.
    /// </summary>
    internal DurableWorkflowAggregate ProjectEvents(IReadOnlyList<DurableWorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var projected = new DurableWorkflowAggregate(CaptureState());

        foreach (var workflowEvent in events)
        {
            projected.Apply(workflowEvent);
        }

        return projected;
    }

    internal IReadOnlyList<ProjectionWrite> CreateProjectionWrites(
        IReadOnlyList<DurableWorkflowEvent> events,
        CheckpointWrite? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0 && checkpoint is null)
        {
            return [];
        }

        var projected = ProjectEvents(events);
        if (checkpoint is not null)
        {
            DurableWorkflowReplayApplier.ApplyStructuredEnvelopeStatus(
                projected,
                new DurableCheckpointPayload
                {
                    ContentType = checkpoint.ContentType,
                    Payload = checkpoint.Payload
                });
        }

        var snapshot = projected.ToInstanceSnapshot();
        return snapshot is null
            ? []
            : [new ProjectionWrite(projected.InstanceId, ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = snapshot
            }];
    }

    private void Apply(DurableWorkflowEvent workflowEvent)
    {
        DurableWorkflowReplayApplier.Apply(this, workflowEvent);
    }

    internal static CausationId ToCausationId(CommandId commandId)
    {
        return new CausationId(commandId.Value);
    }

    internal DurableResourcePoolEventContext CreateResourcePoolEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        FiberId? fiberId = null,
        ScopeId? scopeId = null,
        long waitSequence = 0)
    {
        return new DurableResourcePoolEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId,
            fiberId,
            scopeId,
            waitSequence);
    }

    internal IReadOnlyList<WorkflowResourcePoolReleasedEvent> ReleaseEvents(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset occurredAt,
        string? holderKey = null)
    {
        return resourcePoolState.CreateReleaseEvents(
            CreateResourcePoolEventContext(commandId, instanceId, occurredAt),
            holderKey);
    }

    internal WorkflowRuntimeCheckpointState ToCheckpointRuntimeState()
    {
        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers = timerState.CreateCheckpointActiveTimers(),
            ActiveWaits = waitState.CreateCheckpointActiveWaits(),
            ActiveResourceTickets = resourcePoolState.CreateCheckpointActiveResourceTickets(),
            PendingResumes = waitState.CreateCheckpointPendingResumes(),
            ContinuationFailureCount = ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = ContinuationRetryNotBefore
        };
    }

    /// <summary>
    /// Creates the empty runtime-state baseline used by continue-as-new.
    /// </summary>
    internal WorkflowRuntimeCheckpointState ToContinueAsNewCheckpointRuntimeState()
    {
        return new WorkflowRuntimeCheckpointState();
    }

    private ProjectionWorkflowInstanceSnapshot? ToInstanceSnapshot()
    {
        if (DefinitionId is not { } definitionId ||
            DefinitionVersion is not { } definitionVersion ||
            Status is not { } status ||
            CreatedAt is not { } createdAt ||
            UpdatedAt is not { } updatedAt)
        {
            return null;
        }

        return new ProjectionWorkflowInstanceSnapshot
        {
            InstanceId = InstanceId,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId,
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion,
            Status = status,
            StreamVersion = StreamVersion.Value,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            LastActiveAt = updatedAt,
            ErrorSummary = ErrorSummary,
            EndOutcomeName = OutcomeName,
            ContinueAsNewGeneration = ContinueAsNewGeneration,
            ActiveWaits = waitState.CreateActiveWaitSnapshots()
        };
    }

    internal void ApplyResourcePoolReplayEffects(DurableResourcePoolReplayEffects effects)
    {
        foreach (var wait in effects.WaitsToRegister)
        {
            waitState.Register(wait);
        }
    }
}
