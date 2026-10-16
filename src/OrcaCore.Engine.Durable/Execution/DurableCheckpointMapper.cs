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
                    timer.RegisteredAt)
                {
                    FiberId = timer.FiberId,
                    ScopeId = timer.ScopeId
                })
                .ToArray(),
            checkpoint.RuntimeState.ActiveWaits
                .Select(wait => new DurableActiveWait(
                    wait.WaitId,
                    wait.EventName,
                    wait.CorrelationId,
                    wait.RegisteredAt,
                    DurableWaitResidency.FromProtocol(wait.Mode),
                    wait.BranchId,
                    wait.TimeoutTimerId)
                {
                    EventContractVersion = wait.EventContractVersion,
                    WaitSequence = wait.WaitSequence,
                    FiberId = wait.FiberId,
                    ScopeId = wait.ScopeId
                })
                .ToArray(),
            checkpoint.RuntimeState.ActiveResourceTickets,
            checkpoint.ContentType,
            [.. checkpoint.Payload])
        {
            PendingResumes = checkpoint.RuntimeState.PendingResumes
                .Select(pending => new DurablePendingResume(
                    pending.WaitId,
                    pending.MatchedEventId,
                    pending.EventName,
                    pending.CorrelationId,
                    pending.BranchId,
                    pending.PayloadContentType,
                    pending.Payload,
                    pending.MatchedAt)
                {
                    EventContractVersion = pending.EventContractVersion,
                    WaitSequence = pending.WaitSequence,
                    FiberId = pending.FiberId,
                    ScopeId = pending.ScopeId
                })
                .ToArray(),
            ContinuationFailureCount = checkpoint.RuntimeState.ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion =
                checkpoint.RuntimeState.ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = checkpoint.RuntimeState.ContinuationRetryNotBefore
        };
    }
}
