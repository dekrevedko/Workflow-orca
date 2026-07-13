using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWorkflowAggregate
{
    private readonly DurableChildWorkflowState childState;
    private readonly DurableExternalJobState externalJobState;
    private readonly DurableResourcePoolState resourcePoolState;
    private readonly DurableTimerState timerState;
    private readonly DurableWaitState waitState;
    private readonly DurableSagaState sagaState;

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
        timerState = DurableTimerState.FromSnapshot(state.ActiveTimers, state.BufferedTimers);
        waitState = DurableWaitState.FromSnapshot(state.ActiveWaits, state.BufferedDeliveries, state.PendingResumes);
        childState = DurableChildWorkflowState.FromSnapshot(
            state.ActiveChildren,
            state.ActiveChildGroups,
            [],
            [],
            state.RecordedParentResumeTokens,
            state.ConsumedParentResumeTokens);
        resourcePoolState = DurableResourcePoolState.FromSnapshot(state.ActiveResourceTickets);
        externalJobState = DurableExternalJobState.FromSnapshot(state.ActiveExternalJobs);
        sagaState = DurableSagaState.FromSnapshot(
            state.CompletedSagaForwardActions,
            state.SagaCompensationActions,
            state.SagaRecoveryInterventions,
            state.RequestedSagaCompensationScopes);
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

    internal DurableChildWorkflowState ChildState => childState;

    internal DurableExternalJobState ExternalJobState => externalJobState;

    internal DurableResourcePoolState ResourcePoolState => resourcePoolState;

    internal DurableTimerState TimerState => timerState;

    internal DurableWaitState WaitState => waitState;

    internal DurableSagaState SagaState => sagaState;

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
        waitState.BufferedDeliveries,
        timerState.BufferedTimers,
        childState.ActiveChildren,
        childState.ActiveChildGroups,
        resourcePoolState.ActiveTickets,
        externalJobState.ActiveJobs,
        sagaState.CompletedForwardActions,
        sagaState.CompensationActions,
        sagaState.RecoveryInterventions,
        sagaState.RequestedCompensationScopes)
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
            or WorkflowStatus.Compensated
            or WorkflowStatus.CompensationFailed;

    internal static DurableWorkflowAggregate Empty(InstanceId instanceId)
    {
        return new DurableWorkflowAggregate(new DurableAggregateState { InstanceId = instanceId });
    }

    internal static DurableWorkflowAggregate Rehydrate(
        DurableAggregateCheckpoint? checkpoint,
        IEnumerable<WorkflowEvent> tail)
    {
        ArgumentNullException.ThrowIfNull(tail);

        var aggregate = checkpoint is null
            ? Empty(default)
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
            BufferedDeliveries = checkpoint.BufferedDeliveries,
            BufferedTimers = checkpoint.BufferedTimers,
            ActiveChildren = checkpoint.ActiveChildren,
            ActiveChildGroups = checkpoint.ActiveChildGroups,
            ActiveResourceTickets = checkpoint.ActiveResourceTickets,
            ActiveExternalJobs = checkpoint.ActiveExternalJobs,
            CompletedSagaForwardActions = checkpoint.CompletedSagaForwardActions,
            SagaCompensationActions = checkpoint.SagaCompensationActions,
            SagaRecoveryInterventions = checkpoint.SagaRecoveryInterventions,
            RequestedSagaCompensationScopes = checkpoint.RequestedSagaCompensationScopes,
            RecordedParentResumeTokens = checkpoint.RecordedParentResumeTokens,
            ConsumedParentResumeTokens = checkpoint.ConsumedParentResumeTokens,
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
            BufferedDeliveries = waitState.BufferedDeliveries,
            BufferedTimers = timerState.BufferedTimers,
            ActiveChildren = childState.ActiveChildren,
            ActiveChildGroups = childState.ActiveChildGroups,
            ActiveResourceTickets = resourcePoolState.ActiveTickets,
            ActiveExternalJobs = externalJobState.ActiveJobs,
            CompletedSagaForwardActions = sagaState.CompletedForwardActions,
            SagaCompensationActions = sagaState.CompensationActions,
            SagaRecoveryInterventions = sagaState.RecoveryInterventions,
            RequestedSagaCompensationScopes = sagaState.RequestedCompensationScopes,
            RecordedParentResumeTokens = childState.RecordedParentResumeTokens,
            ConsumedParentResumeTokens = childState.ConsumedParentResumeTokens,
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
            waitState.BufferedDeliveries,
            timerState.BufferedTimers,
            childState.ActiveChildren,
            childState.ActiveChildGroups,
            resourcePoolState.ActiveTickets,
            externalJobState.ActiveJobs,
            sagaState.CompletedForwardActions,
            sagaState.CompensationActions,
            sagaState.RecoveryInterventions,
            sagaState.RequestedCompensationScopes,
            childState.RecordedParentResumeTokens,
            childState.ConsumedParentResumeTokens,
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

    internal DurableDecision DecideYield(DurableYieldCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideWaitRegistered(DurableWaitRegisteredCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideCancel(CancelWorkflowCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideConsumeParentResumeToken(ConsumeParentResumeTokenCommand command) =>
        DurableChildWorkflowCommandHandler.Handle(this, command);

    internal DurableDecision DecideWaitMatched(DurableWaitMatchedCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideTimerScheduled(ScheduleTimerCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideTimerFired(FireTimerCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecideDeliverEvent(DeliverEventCommand command) =>
        DurableWaitTimerCommandHandler.Handle(this, command);

    internal DurableDecision DecidePause(DurablePauseCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideResume(DurableResumeCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideComplete(DurableCompleteCommand command) =>
        DurableLifecycleCommandHandler.Handle(this, command);

    internal DurableDecision DecideFail(DurableFailCommand command) =>
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

    internal DurableDecision DecideRunChild(DurableRunChildCommand command) =>
        DurableChildWorkflowCommandHandler.Handle(this, command);

    internal DurableDecision DecideChildCompleted(DurableChildCompletedCommand command) =>
        DurableChildWorkflowCommandHandler.Handle(this, command);

    internal DurableDecision DecideRunChildren(DurableRunChildrenCommand command) =>
        DurableChildWorkflowCommandHandler.Handle(this, command);

    internal DurableDecision DecideCompensateChildGroup(CompensateChildGroupCommand command) =>
        DurableChildWorkflowCommandHandler.Handle(this, command);

    internal DurableDecision DecideResourcePoolAcquire(
        AcquireResourcePoolCommand command,
        ResourcePoolAcquireResult acquireResult) =>
        DurableResourcePoolCommandHandler.Handle(this, command, acquireResult);

    internal DurableDecision DecideRunExternalJob(
        RunExternalJobCommand command,
        ResourcePoolAcquireResult? acquireResult) =>
        DurableExternalJobCommandHandler.Handle(this, command, acquireResult);

    internal DurableDecision DecideExternalJobCompleted(CompleteExternalJobCommand command) =>
        DurableExternalJobCommandHandler.Handle(this, command);

    internal DurableDecision DecideExternalJobTimedOut(TimeoutExternalJobCommand command) =>
        DurableExternalJobCommandHandler.Handle(this, command);

    internal DurableDecision DecideRecordSagaForwardActionCompleted(
        RecordSagaForwardActionCompletedCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    internal DurableDecision DecideRequestSagaCompensation(RequestSagaCompensationCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    internal DurableDecision DecideSagaForwardActionTimedOut(SagaForwardActionTimedOutCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    internal DurableDecision DecideCompleteSagaCompensation(CompleteSagaCompensationCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    internal DurableDecision DecideFailSagaCompensation(FailSagaCompensationCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    internal DurableDecision DecideRecordSagaManualRecovery(RecordSagaManualRecoveryCommand command) =>
        DurableSagaCommandHandler.Handle(this, command);

    /// <summary>
    /// Creates a detached copy of this aggregate and applies the supplied uncommitted events to
    /// it, so decisions can materialize post-commit state (checkpoints, projections) without
    /// mutating the decision-time aggregate.
    /// </summary>
    internal DurableWorkflowAggregate ProjectEvents(IReadOnlyList<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var projected = new DurableWorkflowAggregate(CaptureState());

        foreach (var workflowEvent in events)
        {
            projected.Apply(workflowEvent);
        }

        return projected;
    }

    internal IReadOnlyList<ProjectionWrite> CreateProjectionWrites(IReadOnlyList<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return [];
        }

        var projected = ProjectEvents(events);

        var snapshot = projected.ToInstanceSnapshot();
        return snapshot is null
            ? []
            : [new ProjectionWrite(projected.InstanceId, ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = snapshot
            }];
    }

    private void Apply(WorkflowEvent workflowEvent)
    {
        DurableWorkflowReplayApplier.Apply(this, workflowEvent);
    }

    internal static CausationId ToCausationId(CommandId commandId)
    {
        return new CausationId(commandId.Value);
    }

    internal static DurableWaitEventContext CreateWaitEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableWaitEventContext(commandId, instanceId, requestedAt);
    }

    internal static DurableTimerEventContext CreateTimerEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableTimerEventContext(commandId, instanceId, requestedAt);
    }

    internal DurableExternalJobEventContext CreateExternalJobEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableExternalJobEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
    }

    internal DurableResourcePoolEventContext CreateResourcePoolEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableResourcePoolEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
    }

    internal DurableChildWorkflowEventContext CreateChildWorkflowEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableChildWorkflowEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
    }

    internal DurableSagaEventContext CreateSagaEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableSagaEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
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
            BufferedDeliveries = waitState.CreateCheckpointBufferedDeliveries(),
            BufferedTimers = timerState.CreateCheckpointBufferedTimers(),
            ActiveChildren = childState.CreateCheckpointActiveChildren(),
            ActiveChildGroups = childState.CreateCheckpointActiveChildGroups(),
            ActiveResourceTickets = resourcePoolState.CreateCheckpointActiveResourceTickets(),
            ActiveExternalJobs = externalJobState.CreateCheckpointActiveExternalJobs(),
            CompletedSagaForwardActions = sagaState.CreateCheckpointForwardActions(),
            SagaCompensationActions = sagaState.CreateCheckpointCompensationActions(),
            SagaRecoveryInterventions = sagaState.CreateCheckpointRecoveryInterventions(),
            RequestedSagaCompensationScopes = sagaState.RequestedCompensationScopes,
            RecordedParentResumeTokens = childState.RecordedParentResumeTokens,
            ConsumedParentResumeTokens = childState.ConsumedParentResumeTokens,
            PendingResumes = waitState.CreateCheckpointPendingResumes(),
            ContinuationFailureCount = ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = ContinuationRetryNotBefore
        };
    }

    /// <summary>
    /// Creates checkpoint runtime state carrying only the collections continue-as-new preserves:
    /// replay clears active work but keeps saga records and resume-token consumption facts.
    /// </summary>
    internal WorkflowRuntimeCheckpointState ToContinueAsNewCheckpointRuntimeState()
    {
        return new WorkflowRuntimeCheckpointState
        {
            CompletedSagaForwardActions = sagaState.CreateCheckpointForwardActions(),
            SagaCompensationActions = sagaState.CreateCheckpointCompensationActions(),
            SagaRecoveryInterventions = sagaState.CreateCheckpointRecoveryInterventions(),
            RequestedSagaCompensationScopes = sagaState.RequestedCompensationScopes,
            RecordedParentResumeTokens = childState.RecordedParentResumeTokens,
            ConsumedParentResumeTokens = childState.ConsumedParentResumeTokens
        };
    }

    private WorkflowInstanceSnapshot? ToInstanceSnapshot()
    {
        if (DefinitionId is not { } definitionId ||
            DefinitionVersion is not { } definitionVersion ||
            Status is not { } status ||
            CreatedAt is not { } createdAt ||
            UpdatedAt is not { } updatedAt)
        {
            return null;
        }

        return new WorkflowInstanceSnapshot
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
            ErrorSummary = ErrorSummary,
            EndOutcomeName = OutcomeName,
            ContinueAsNewGeneration = ContinueAsNewGeneration,
            ActiveWaits = waitState.CreateActiveWaitSnapshots(),
            SagaAudits = sagaState.CreateAuditScopes(Status)
        };
    }

    internal void ApplyChildReplayEffects(DurableChildReplayEffects effects)
    {
        foreach (var wait in effects.WaitsToRegister)
        {
            waitState.Register(wait);
        }

        foreach (var waitId in effects.WaitIdsToRemove)
        {
            waitState.Remove(waitId);
        }

        ErrorSummary = effects.PropagatedFailureErrorSummary ?? ErrorSummary;
    }

    internal void ApplyResourcePoolReplayEffects(DurableResourcePoolReplayEffects effects)
    {
        foreach (var wait in effects.WaitsToRegister)
        {
            waitState.Register(wait);
        }
    }
}
