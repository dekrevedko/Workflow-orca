using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal static class DurableCheckpointMapper
{
    internal static DurableAggregateCheckpoint ToAggregateCheckpoint(CheckpointWrite checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        return new DurableAggregateCheckpoint(
            checkpoint.InstanceId,
            checkpoint.StreamVersion,
            checkpoint.ParentInstanceId,
            checkpoint.RootInstanceId,
            checkpoint.DefinitionId,
            checkpoint.DefinitionVersion,
            checkpoint.Status,
            null,
            null,
            checkpoint.LastStepPath,
            checkpoint.ErrorSummary,
            checkpoint.OutcomeName,
            checkpoint.ContinueAsNewGeneration,
            checkpoint.RuntimeState.ActiveTimers
                .Select(timer => new DurableActiveTimer(
                    timer.TimerId,
                    timer.FireAt,
                    timer.WakeupName,
                    timer.RegisteredAt))
                .ToArray(),
            checkpoint.RuntimeState.ActiveWaits
                .Select(wait => new DurableActiveWait(
                    wait.WaitId,
                    wait.EventName,
                    wait.CorrelationId,
                    wait.RegisteredAt,
                    wait.Mode,
                    wait.BranchId))
                .ToArray(),
            checkpoint.RuntimeState.BufferedDeliveries
                .Select(delivery => new DurableBufferedDelivery(
                    delivery.EventId,
                    delivery.EventName,
                    delivery.CorrelationId,
                    delivery.BranchId))
                .ToArray(),
            checkpoint.RuntimeState.BufferedTimers
                .Select(timer => new DurableBufferedTimer(
                    timer.TimerId,
                    timer.WakeupName,
                    timer.BufferedAt))
                .ToArray(),
            checkpoint.RuntimeState.ActiveChildren
                .Select(child => new DurableActiveChild(
                    child.GroupId,
                    child.ChildInstanceId,
                    child.WaitId,
                    child.FailurePolicy,
                    child.JoinPolicy,
                    child.ResidualPolicy,
                    child.ItemSnapshot))
                .ToArray(),
            checkpoint.RuntimeState.ActiveChildGroups
                .Select(group => new DurableActiveChildGroup(
                    group.GroupId,
                    group.FailurePolicy,
                    group.JoinPolicy,
                    group.ResidualPolicy,
                    group.MaxConcurrency,
                    group.NextDispatchIndex,
                    group.Children))
                .ToArray(),
            checkpoint.RuntimeState.ActiveResourceTickets,
            checkpoint.RuntimeState.ActiveExternalJobs
                .Select(job => new DurableActiveExternalJob(
                    job.ExternalJobId,
                    job.WaitId,
                    job.TimeoutTimerId))
                .ToArray(),
            checkpoint.RuntimeState.CompletedSagaForwardActions
                .Select(action => new DurableSagaForwardAction(
                    action.ScopeId,
                    action.ActionKey,
                    action.CompensationKey,
                    action.CompletedAt))
                .ToArray(),
            checkpoint.RuntimeState.SagaCompensationActions
                .Select(action => new DurableSagaCompensationAction(
                    action.ScopeId,
                    action.ActionKey,
                    action.Order,
                    action.StartedAt,
                    action.CompletedAt,
                    action.FailedAt,
                    action.ErrorSummary,
                    action.Status))
                .ToArray(),
            checkpoint.RuntimeState.SagaRecoveryInterventions
                .Select(intervention => new DurableSagaRecoveryIntervention(
                    intervention.ScopeId,
                    intervention.ActionKey,
                    intervention.OperatorId,
                    intervention.RecoveryAction,
                    intervention.Reason,
                    intervention.RecordedAt,
                    intervention.TargetStatus))
                .ToArray(),
            checkpoint.RuntimeState.RequestedSagaCompensationScopes,
            checkpoint.RuntimeState.RecordedParentResumeTokens,
            checkpoint.RuntimeState.ConsumedParentResumeTokens,
            checkpoint.ContentType,
            [.. checkpoint.Payload]);
    }
}
