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

    private DurableWorkflowAggregate(
        InstanceId instanceId,
        StreamVersion streamVersion,
        InstanceId? parentInstanceId,
        InstanceId? rootInstanceId,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt,
        string? lastStepPath,
        string? errorSummary,
        string? outcomeName,
        int continueAsNewGeneration,
        IEnumerable<DurableActiveTimer> activeTimers,
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries,
        IEnumerable<DurableBufferedTimer> bufferedTimers,
        IEnumerable<DurableActiveChild> activeChildren,
        IEnumerable<DurableActiveChildGroup> activeChildGroups,
        IEnumerable<ResourcePoolTicket> activeResourceTickets,
        IEnumerable<DurableActiveExternalJob> activeExternalJobs,
        IEnumerable<DurableSagaForwardAction> completedSagaForwardActions,
        IEnumerable<DurableSagaCompensationAction> sagaCompensationActions,
        IEnumerable<DurableSagaRecoveryIntervention> sagaRecoveryInterventions,
        IEnumerable<string> requestedSagaCompensationScopes,
        IEnumerable<EventId> recordedParentResumeTokens,
        IEnumerable<EventId> consumedParentResumeTokens)
    {
        InstanceId = instanceId;
        StreamVersion = streamVersion;
        ParentInstanceId = parentInstanceId;
        RootInstanceId = rootInstanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastStepPath = lastStepPath;
        ErrorSummary = errorSummary;
        OutcomeName = outcomeName;
        ContinueAsNewGeneration = continueAsNewGeneration;
        timerState = DurableTimerState.FromSnapshot(activeTimers, bufferedTimers);
        waitState = DurableWaitState.FromSnapshot(activeWaits, bufferedDeliveries);
        childState = DurableChildWorkflowState.FromSnapshot(
            activeChildren,
            activeChildGroups,
            [],
            [],
            recordedParentResumeTokens,
            consumedParentResumeTokens);
        resourcePoolState = DurableResourcePoolState.FromSnapshot(activeResourceTickets);
        externalJobState = DurableExternalJobState.FromSnapshot(activeExternalJobs);
        sagaState = DurableSagaState.FromSnapshot(
            completedSagaForwardActions,
            sagaCompensationActions,
            sagaRecoveryInterventions,
            requestedSagaCompensationScopes);
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
        sagaState.RequestedCompensationScopes);

    internal bool IsTerminal =>
        Status is WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.Compensated
            or WorkflowStatus.CompensationFailed;

    internal static DurableWorkflowAggregate Empty(InstanceId instanceId)
    {
        return new DurableWorkflowAggregate(
            instanceId,
            StreamVersion.Empty,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    internal static DurableWorkflowAggregate Rehydrate(
        DurableAggregateCheckpoint? checkpoint,
        IEnumerable<WorkflowEvent> tail)
    {
        ArgumentNullException.ThrowIfNull(tail);

        var aggregate = checkpoint is null
            ? Empty(default)
            : new DurableWorkflowAggregate(
                checkpoint.InstanceId,
                checkpoint.StreamVersion,
                checkpoint.ParentInstanceId,
                checkpoint.RootInstanceId,
                checkpoint.DefinitionId,
                checkpoint.DefinitionVersion,
                checkpoint.Status,
                checkpoint.CreatedAt,
                checkpoint.UpdatedAt,
                checkpoint.LastStepPath,
                checkpoint.ErrorSummary,
                checkpoint.OutcomeName,
                checkpoint.ContinueAsNewGeneration,
                checkpoint.ActiveTimers,
                checkpoint.ActiveWaits,
                checkpoint.BufferedDeliveries,
                checkpoint.BufferedTimers,
                checkpoint.ActiveChildren,
                checkpoint.ActiveChildGroups,
                checkpoint.ActiveResourceTickets,
                checkpoint.ActiveExternalJobs,
                checkpoint.CompletedSagaForwardActions,
                checkpoint.SagaCompensationActions,
                checkpoint.SagaRecoveryInterventions,
                checkpoint.RequestedSagaCompensationScopes,
                checkpoint.RecordedParentResumeTokens,
                checkpoint.ConsumedParentResumeTokens);

        foreach (var workflowEvent in tail)
        {
            aggregate.Apply(workflowEvent);
        }

        return aggregate;
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
            [.. payload]);
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

    internal IReadOnlyList<ProjectionWrite> CreateProjectionWrites(IReadOnlyList<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return [];
        }

        var projected = new DurableWorkflowAggregate(
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
            childState.ConsumedParentResumeTokens);

        foreach (var workflowEvent in events)
        {
            projected.Apply(workflowEvent);
        }

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
            ConsumedParentResumeTokens = childState.ConsumedParentResumeTokens
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
