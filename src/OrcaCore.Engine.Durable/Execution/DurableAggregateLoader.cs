using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using DurableWorkflowEvent = OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableAggregateLoader
{
    private readonly IWorkflowEventStore eventStore;

    public DurableAggregateLoader(IWorkflowEventStore eventStore)
    {
        ArgumentNullException.ThrowIfNull(eventStore);
        this.eventStore = eventStore;
    }

    public async Task<DurableWorkflowAggregate> LoadAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        (await LoadAsync(instanceId, requireExecutionPosition: false, cancellationToken).ConfigureAwait(false)).Aggregate;

    internal Task<DurableLoadedExecutionState> LoadExecutionStateAsync(
        InstanceId instanceId, CancellationToken cancellationToken) =>
        LoadAsync(instanceId, requireExecutionPosition: true, cancellationToken);

    private async Task<DurableLoadedExecutionState> LoadAsync(
        InstanceId instanceId, bool requireExecutionPosition, CancellationToken cancellationToken)
    {
        var checkpointOption = await eventStore
            .LoadCheckpointAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        var checkpointVersion = checkpointOption.HasValue
            ? checkpointOption.Value.StreamVersion
            : StreamVersion.Empty;
        var tail = await eventStore
            .LoadTailAsync(new WorkflowStreamId(instanceId), checkpointVersion, cancellationToken)
            .ConfigureAwait(false);

        var checkpoint = checkpointOption.HasValue ? DurableCheckpointMapper.ToAggregateCheckpoint(checkpointOption.Value) : null;
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            instanceId,
            checkpoint,
            tail);
        if (!requireExecutionPosition || tail.Count == 0 || aggregate.IsTerminal)
            return new(checkpointOption, aggregate);
        // Only facts reconstructable against this checkpoint's blocked obligations may
        // advance the aggregate while keeping its execution envelope. Check the whole tail:
        // a safe wake does not make a later position-changing commit safe to resume.
        var baseline = DurableWorkflowAggregate.Rehydrate(instanceId, checkpoint, []);
        var needsReload = tail.FirstOrDefault(workflowEvent => !(workflowEvent switch
        {
            WorkflowStartedEvent => !checkpointOption.HasValue && ReferenceEquals(workflowEvent, tail[0]),
            WorkflowCancellationRequestedEvent or WorkflowParkedEvent or WorkflowUnparkedEvent or
                WorkflowContinuationAttemptFailedEvent or WorkflowContinuationAttemptResetEvent => true,
            WorkflowWaitMatchedEvent matched => baseline.WaitState.ActiveWaits.Any(wait => wait.WaitId.Equals(matched.WaitId)),
            WorkflowTimerFiredEvent fired => baseline.TimerState.ActiveTimers.Any(timer => timer.TimerId.Equals(fired.TimerId)),
            WorkflowTimerCancelledEvent cancelled => baseline.WaitState.ActiveWaits.Any(wait =>
                wait.TimeoutTimerId == cancelled.TimerId && tail.OfType<WorkflowWaitMatchedEvent>().Any(matched =>
                    matched.WaitId.Equals(wait.WaitId) && matched.CommandId.Equals(cancelled.CommandId))),
            WorkflowWaitCancelledEvent cancelled => baseline.WaitState.ActiveWaits.Any(wait =>
                wait.WaitId.Equals(cancelled.WaitId) && tail.OfType<WorkflowTimerFiredEvent>().Any(fired =>
                    wait.TimeoutTimerId == fired.TimerId && fired.CommandId.Equals(cancelled.CommandId))),
            _ => false
        }));
        return new(checkpointOption, aggregate, needsReload, needsReload is null ? null : tail);
    }
}

internal sealed record DurableLoadedExecutionState(
    Option<CheckpointWrite> Checkpoint,
    DurableWorkflowAggregate Aggregate,
    DurableWorkflowEvent? EventRequiringReload = null,
    IReadOnlyList<DurableWorkflowEvent>? UnreconciledTail = null)
{
    internal bool NeedsPositionReload => EventRequiringReload is not null;

    internal bool HasSameUnreconciledPositionAs(DurableLoadedExecutionState other)
    {
        if (Checkpoint.HasValue != other.Checkpoint.HasValue || Aggregate.StreamVersion != other.Aggregate.StreamVersion)
            return false;
        if (Checkpoint.HasValue && (Checkpoint.Value.StreamVersion != other.Checkpoint.Value.StreamVersion ||
            Checkpoint.Value.ContentType != other.Checkpoint.Value.ContentType ||
            !Checkpoint.Value.Payload.AsSpan().SequenceEqual(other.Checkpoint.Value.Payload)))
            return false;
        // The provider contract makes facts immutable at a committed version. Compare
        // their full ordered identities as well, rather than relying on a tail count.
        return UnreconciledTail is { } left && other.UnreconciledTail is { } right &&
            left.Count == right.Count && left.Zip(right).All(pair =>
                pair.First.EventId.Equals(pair.Second.EventId) && pair.First.GetType() == pair.Second.GetType());
    }
}
