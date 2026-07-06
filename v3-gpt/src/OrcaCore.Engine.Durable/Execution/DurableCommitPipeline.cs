using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableCommitPipeline(
    IWorkflowEventStore eventStore,
    DurableCommitMaterializer commitMaterializer,
    DurableResourcePoolCommitEffects resourcePoolCommitEffects)
{
    internal async Task<DurableCommandResult> CommitAsync(
        InstanceId instanceId,
        DurableWorkflowAggregate aggregate,
        DurableDecision decision,
        EventId? inboxEventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Events.Count == 0 && decision.Checkpoint is null)
        {
            return await CommitNoMutationAsync(
                instanceId,
                aggregate.StreamVersion,
                decision,
                inboxEventId,
                cancellationToken)
                .ConfigureAwait(false);
        }

        var appendResult = await eventStore
            .AppendAsync(
                commitMaterializer.CreateBatch(
                    instanceId,
                    aggregate.StreamVersion,
                    decision,
                    aggregate,
                    inboxEventId),
                cancellationToken)
            .ConfigureAwait(false);

        if (appendResult.IsFailure)
        {
            await resourcePoolCommitEffects.RollBackAcquiresAsync(decision.Events, cancellationToken).ConfigureAwait(false);
            return new DurableCommandResult(
                DurableCommandOutcome.Conflict,
                appendResult.Error.Message,
                aggregate.StreamVersion);
        }

        try
        {
            await resourcePoolCommitEffects.ReleaseCommittedTicketsAsync(decision.Events, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The events are already durably committed; surfacing a release failure here would make a
            // committed command look failed and provoke conflicting retries. The unreleased ticket is
            // recovered by lease expiry via the operational sweep.
        }

        return new DurableCommandResult(
            DurableCommandOutcome.Committed,
            null,
            appendResult.Value.NewVersion,
            decision.EvictAfterCommit);
    }

    private async Task<DurableCommandResult> CommitNoMutationAsync(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        DurableDecision decision,
        EventId? inboxEventId,
        CancellationToken cancellationToken)
    {
        if (inboxEventId is { } poisonedEventId)
        {
            return await CommitInboxOnlyAsync(
                instanceId,
                expectedVersion,
                poisonedEventId,
                InboxRecordState.Poisoned,
                DurableCommandOutcome.Poisoned,
                "No active wait matched the inbound event.",
                cancellationToken)
                .ConfigureAwait(false);
        }

        if (decision.EvictAfterCommit)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.Evicted,
                "Instance is evictable from hot memory.",
                expectedVersion,
                true);
        }

        return new DurableCommandResult(
            DurableCommandOutcome.NoOp,
            "Command produced no durable events.",
            expectedVersion);
    }

    private async Task<DurableCommandResult> CommitInboxOnlyAsync(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        EventId eventId,
        InboxRecordState state,
        DurableCommandOutcome successOutcome,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var appendResult = await eventStore
            .AppendAsync(
                commitMaterializer.CreateInboxOnlyBatch(instanceId, expectedVersion, eventId, state),
                cancellationToken)
            .ConfigureAwait(false);

        return appendResult.Match(
            success => new DurableCommandResult(successOutcome, successMessage, success.NewVersion),
            error => new DurableCommandResult(DurableCommandOutcome.Conflict, error.Message, expectedVersion));
    }
}
