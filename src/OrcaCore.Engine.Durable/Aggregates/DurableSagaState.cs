using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableSagaState
{
    private readonly List<DurableSagaForwardAction> completedForwardActions;
    private readonly List<DurableSagaCompensationAction> compensationActions;
    private readonly List<DurableSagaRecoveryIntervention> recoveryInterventions;
    private readonly HashSet<string> requestedCompensationScopes;

    private DurableSagaState(
        IEnumerable<DurableSagaForwardAction> completedForwardActions,
        IEnumerable<DurableSagaCompensationAction> compensationActions,
        IEnumerable<DurableSagaRecoveryIntervention> recoveryInterventions,
        IEnumerable<string> requestedCompensationScopes)
    {
        this.completedForwardActions = [.. completedForwardActions];
        this.compensationActions = [.. compensationActions];
        this.recoveryInterventions = [.. recoveryInterventions];
        this.requestedCompensationScopes = new HashSet<string>(requestedCompensationScopes, StringComparer.Ordinal);
    }

    internal IReadOnlyList<DurableSagaForwardAction> CompletedForwardActions => [.. completedForwardActions];

    internal IReadOnlyList<DurableSagaCompensationAction> CompensationActions => [.. compensationActions];

    internal IReadOnlyList<DurableSagaRecoveryIntervention> RecoveryInterventions => [.. recoveryInterventions];

    internal IReadOnlyList<string> RequestedCompensationScopes => [.. requestedCompensationScopes];

    internal static DurableSagaState Empty()
    {
        return FromSnapshot([], [], [], []);
    }

    internal static DurableSagaState FromSnapshot(
        IEnumerable<DurableSagaForwardAction> completedForwardActions,
        IEnumerable<DurableSagaCompensationAction> compensationActions,
        IEnumerable<DurableSagaRecoveryIntervention> recoveryInterventions,
        IEnumerable<string> requestedCompensationScopes)
    {
        ArgumentNullException.ThrowIfNull(completedForwardActions);
        ArgumentNullException.ThrowIfNull(compensationActions);
        ArgumentNullException.ThrowIfNull(recoveryInterventions);
        ArgumentNullException.ThrowIfNull(requestedCompensationScopes);

        return new DurableSagaState(
            completedForwardActions,
            compensationActions,
            recoveryInterventions,
            requestedCompensationScopes);
    }

    internal IReadOnlyList<CheckpointSagaForwardAction> CreateCheckpointForwardActions()
    {
        return completedForwardActions
            .Select(action => new CheckpointSagaForwardAction(
                action.ScopeId,
                action.ActionKey,
                action.CompensationKey,
                action.CompletedAt)
            {
                FiberId = action.FiberId,
                OwningScopeId = action.OwningScopeId,
                EligibleScopeId = action.EligibleScopeId,
                InstructionId = action.InstructionId,
                CommittedSequence = action.CommittedSequence,
                CanonicalBranchOrder = action.CanonicalBranchOrder,
                CanonicalInstructionOrder = action.CanonicalInstructionOrder,
                ScopeOrderOverride = action.ScopeOrderOverride
            })
            .ToArray();
    }

    internal IReadOnlyList<CheckpointSagaCompensationAction> CreateCheckpointCompensationActions()
    {
        return compensationActions
            .Select(action => new CheckpointSagaCompensationAction(
                action.ScopeId,
                action.ActionKey,
                action.Order,
                action.StartedAt,
                action.CompletedAt,
                action.FailedAt,
                action.ErrorSummary,
                action.Status))
            .ToArray();
    }

    internal IReadOnlyList<CheckpointSagaRecoveryIntervention> CreateCheckpointRecoveryInterventions()
    {
        return recoveryInterventions
            .Select(intervention => new CheckpointSagaRecoveryIntervention(
                intervention.ScopeId,
                intervention.ActionKey,
                intervention.OperatorId,
                intervention.RecoveryAction,
                intervention.Reason,
                intervention.RecordedAt,
                intervention.TargetStatus))
            .ToArray();
    }

    internal bool HasForwardAction(string scopeId, string actionKey)
    {
        return completedForwardActions.Any(action =>
            string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal) &&
            string.Equals(action.ActionKey, actionKey, StringComparison.Ordinal));
    }

    internal bool HasRequestedCompensation(string scopeId)
    {
        return requestedCompensationScopes.Contains(scopeId);
    }

    internal IReadOnlyList<WorkflowEvent> PlanCompensation(
        DurableSagaEventContext context,
        string scopeId,
        string? reason,
        IReadOnlyCollection<ScopeId>? coveredExecutionScopeIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);

        var coveredScopes = coveredExecutionScopeIds is null
            ? null
            : new HashSet<ScopeId>(coveredExecutionScopeIds);
        var eligibleActions = completedForwardActions
            .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
            .Where(action => coveredScopes is null ||
                action.EligibleScopeId is { } eligibleScopeId && coveredScopes.Contains(eligibleScopeId))
            .Where(action => !string.IsNullOrWhiteSpace(action.CompensationKey))
            .ToArray();
        return PlanCompensation(context, scopeId, reason, eligibleActions);
    }

    internal IReadOnlyList<WorkflowEvent> PlanCompensationForFailure(
        DurableSagaEventContext context,
        IReadOnlyCollection<FiberId> terminalFiberIds,
        IReadOnlyCollection<ScopeId> failedScopeIds,
        bool coversRootEligibility,
        string? reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(terminalFiberIds);
        ArgumentNullException.ThrowIfNull(failedScopeIds);

        var terminalFibers = new HashSet<FiberId>(terminalFiberIds);
        var failedScopes = new HashSet<ScopeId>(failedScopeIds);
        var affected = completedForwardActions
            .Where(action =>
                action.FiberId is { } fiberId && terminalFibers.Contains(fiberId) ||
                action.EligibleScopeId is { } scopeId && failedScopes.Contains(scopeId) ||
                coversRootEligibility && action.EligibleScopeId is null)
            .Where(action => !string.IsNullOrWhiteSpace(action.CompensationKey))
            .GroupBy(action => action.ScopeId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        var events = new List<WorkflowEvent>();
        foreach (var group in affected)
        {
            if (!HasRequestedCompensation(group.Key))
            {
                events.AddRange(PlanCompensation(context, group.Key, reason, group.ToArray()));
            }
        }

        return events;
    }

    private static IReadOnlyList<WorkflowEvent> PlanCompensation(
        DurableSagaEventContext context,
        string scopeId,
        string? reason,
        DurableSagaForwardAction[] eligibleActions)
    {
        var events = new List<WorkflowEvent>
        {
            new SagaCompensationRequestedEvent
            {
                EventId = EventId.New(),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                ParentInstanceId = context.ParentInstanceId,
                RootInstanceId = context.RootInstanceId,
                ScopeId = scopeId,
                Reason = reason
            }
        };
        eligibleActions = OrderForCompensation(eligibleActions);
        for (var index = 0; index < eligibleActions.Length; index++)
        {
            events.Add(new SagaCompensationStartedEvent
            {
                EventId = EventId.New(),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                ParentInstanceId = context.ParentInstanceId,
                RootInstanceId = context.RootInstanceId,
                ScopeId = scopeId,
                ActionKey = eligibleActions[index].CompensationKey,
                Order = index
            });
        }

        return events;
    }

    internal void Apply(WorkflowEvent workflowEvent, DateTimeOffset? aggregateUpdatedAt = null)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        switch (workflowEvent)
        {
            case SagaForwardActionCompletedEvent sagaForwardActionCompleted:
                completedForwardActions.RemoveAll(action =>
                    string.Equals(action.ScopeId, sagaForwardActionCompleted.ScopeId, StringComparison.Ordinal) &&
                    string.Equals(action.ActionKey, sagaForwardActionCompleted.ActionKey, StringComparison.Ordinal));
                completedForwardActions.Add(new DurableSagaForwardAction(
                    sagaForwardActionCompleted.ScopeId,
                    sagaForwardActionCompleted.ActionKey,
                    sagaForwardActionCompleted.CompensationKey,
                    sagaForwardActionCompleted.OccurredAt)
                {
                    FiberId = sagaForwardActionCompleted.FiberId,
                    OwningScopeId = sagaForwardActionCompleted.OwningScopeId,
                    EligibleScopeId = sagaForwardActionCompleted.OwningScopeId,
                    InstructionId = sagaForwardActionCompleted.InstructionId,
                    CommittedSequence = sagaForwardActionCompleted.CommittedSequence,
                    CanonicalBranchOrder = sagaForwardActionCompleted.CanonicalBranchOrder,
                    CanonicalInstructionOrder = sagaForwardActionCompleted.CanonicalInstructionOrder,
                    ScopeOrderOverride = sagaForwardActionCompleted.ScopeOrderOverride
                });
                break;
            case SagaForwardActionsTransferredEvent transferred:
                for (var index = 0; index < completedForwardActions.Count; index++)
                {
                    var action = completedForwardActions[index];
                    if (action.EligibleScopeId == transferred.FromExecutionScopeId)
                    {
                        completedForwardActions[index] = action with
                        {
                            EligibleScopeId = transferred.ToExecutionScopeId
                        };
                    }
                }

                break;
            case SagaForwardActionTimedOutEvent:
                break;
            case SagaCompensationRequestedEvent sagaCompensationRequested:
                requestedCompensationScopes.Add(sagaCompensationRequested.ScopeId);
                break;
            case SagaCompensationStartedEvent sagaCompensationStarted:
                compensationActions.RemoveAll(action =>
                    string.Equals(action.ScopeId, sagaCompensationStarted.ScopeId, StringComparison.Ordinal) &&
                    string.Equals(action.ActionKey, sagaCompensationStarted.ActionKey, StringComparison.Ordinal));
                compensationActions.Add(new DurableSagaCompensationAction(
                    sagaCompensationStarted.ScopeId,
                    sagaCompensationStarted.ActionKey,
                    sagaCompensationStarted.Order,
                    sagaCompensationStarted.OccurredAt,
                    null,
                    null,
                    null,
                    SagaCompensationActionStatus.Started));
                break;
            case SagaCompensationCompletedEvent sagaCompensationCompleted:
                UpdateCompensationAction(
                    sagaCompensationCompleted.ScopeId,
                    sagaCompensationCompleted.ActionKey,
                    sagaCompensationCompleted.OccurredAt,
                    null,
                    null,
                    SagaCompensationActionStatus.Completed,
                    aggregateUpdatedAt);
                break;
            case SagaCompensationFailedEvent sagaCompensationFailed:
                UpdateCompensationAction(
                    sagaCompensationFailed.ScopeId,
                    sagaCompensationFailed.ActionKey,
                    null,
                    sagaCompensationFailed.OccurredAt,
                    sagaCompensationFailed.ErrorSummary,
                    SagaCompensationActionStatus.Failed,
                    aggregateUpdatedAt);
                break;
            case SagaManualRecoveryRecordedEvent sagaManualRecoveryRecorded:
                recoveryInterventions.Add(new DurableSagaRecoveryIntervention(
                    sagaManualRecoveryRecorded.ScopeId,
                    sagaManualRecoveryRecorded.ActionKey,
                    sagaManualRecoveryRecorded.OperatorId,
                    sagaManualRecoveryRecorded.RecoveryAction,
                    sagaManualRecoveryRecorded.Reason,
                    sagaManualRecoveryRecorded.OccurredAt,
                    sagaManualRecoveryRecorded.TargetStatus));
                break;
        }
    }

    internal bool AllCompensationsCompleteAfter(string scopeId, string completedActionKey)
    {
        var actions = compensationActions
            .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
            .ToArray();
        if (actions.Length == 0)
        {
            return false;
        }

        var completedCount = actions.Count(action =>
            action.Status == SagaCompensationActionStatus.Completed ||
            string.Equals(action.ActionKey, completedActionKey, StringComparison.Ordinal));
        return completedCount == actions.Length;
    }

    internal IReadOnlyList<SagaAuditScopeSnapshot> CreateAuditScopes(WorkflowStatus? aggregateStatus)
    {
        var scopeIds = completedForwardActions.Select(action => action.ScopeId)
            .Concat(compensationActions.Select(action => action.ScopeId))
            .Concat(recoveryInterventions.Select(intervention => intervention.ScopeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scopeId => scopeId, StringComparer.Ordinal)
            .ToArray();

        return scopeIds.Select(scopeId => new SagaAuditScopeSnapshot
        {
            ScopeId = scopeId,
            Outcome = aggregateStatus is WorkflowStatus.Compensated or WorkflowStatus.CompensationFailed
                ? aggregateStatus
                : null,
            ForwardActions = completedForwardActions
                .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(action => action.CompletedAt)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .Select(action => new SagaForwardActionSnapshot
                {
                    ScopeId = action.ScopeId,
                    ActionKey = action.ActionKey,
                    CompensationKey = action.CompensationKey,
                    CompletedAt = action.CompletedAt
                })
                .ToArray(),
            CompensationActions = compensationActions
                .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(action => action.Order)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .Select(action => new SagaCompensationActionSnapshot
                {
                    ScopeId = action.ScopeId,
                    ActionKey = action.ActionKey,
                    Order = action.Order,
                    StartedAt = action.StartedAt,
                    CompletedAt = action.CompletedAt,
                    FailedAt = action.FailedAt,
                    ErrorSummary = action.ErrorSummary,
                    Status = action.Status
                })
                .ToArray(),
            RecoveryInterventions = recoveryInterventions
                .Where(intervention => string.Equals(intervention.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(intervention => intervention.RecordedAt)
                .ThenBy(intervention => intervention.ActionKey, StringComparer.Ordinal)
                .Select(intervention => new SagaRecoveryInterventionSnapshot
                {
                    ScopeId = intervention.ScopeId,
                    ActionKey = intervention.ActionKey,
                    OperatorId = intervention.OperatorId,
                    RecoveryAction = intervention.RecoveryAction,
                    Reason = intervention.Reason,
                    RecordedAt = intervention.RecordedAt,
                    TargetStatus = intervention.TargetStatus
                })
                .ToArray()
        }).ToArray();
    }

    private void UpdateCompensationAction(
        string scopeId,
        string actionKey,
        DateTimeOffset? completedAt,
        DateTimeOffset? failedAt,
        string? errorSummary,
        SagaCompensationActionStatus status,
        DateTimeOffset? aggregateUpdatedAt)
    {
        var existing = compensationActions.FirstOrDefault(action =>
            string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal) &&
            string.Equals(action.ActionKey, actionKey, StringComparison.Ordinal));
        if (existing is null)
        {
            compensationActions.Add(new DurableSagaCompensationAction(
                scopeId,
                actionKey,
                0,
                completedAt ?? failedAt ?? aggregateUpdatedAt ?? DateTimeOffset.MinValue,
                completedAt,
                failedAt,
                errorSummary,
                status));
            return;
        }

        compensationActions.Remove(existing);
        compensationActions.Add(existing with
        {
            CompletedAt = completedAt ?? existing.CompletedAt,
            FailedAt = failedAt ?? existing.FailedAt,
            ErrorSummary = errorSummary ?? existing.ErrorSummary,
            Status = status
        });
    }

    private static DurableSagaForwardAction[] OrderForCompensation(
        DurableSagaForwardAction[] eligibleActions)
    {
        var hasOverride = eligibleActions.Any(action => action.ScopeOrderOverride.HasValue);
        if (hasOverride && eligibleActions.Any(action => !action.ScopeOrderOverride.HasValue))
        {
            throw new InvalidOperationException(
                "A plan-bound saga compensation order override must cover every eligible action in the scope.");
        }

        return hasOverride
            ? eligibleActions
                .OrderByDescending(action => action.ScopeOrderOverride)
                .ThenByDescending(action => action.CommittedSequence)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .ToArray()
            : eligibleActions
                .OrderByDescending(action => action.CanonicalBranchOrder)
                .ThenByDescending(action => action.CanonicalInstructionOrder)
                .ThenByDescending(action => action.CommittedSequence)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .ToArray();
    }
}

internal sealed record DurableSagaEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId? ParentInstanceId,
    InstanceId RootInstanceId)
{
    internal CausationId CausationId => new(CommandId.Value);
}
