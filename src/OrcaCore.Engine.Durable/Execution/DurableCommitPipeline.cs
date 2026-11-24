using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Dag;

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
        DurableInboxDelivery? inboxDelivery,
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
                inboxDelivery,
                cancellationToken)
                .ConfigureAwait(false);
        }

        var batch = commitMaterializer.CreateBatch(instanceId, aggregate.StreamVersion, decision, aggregate, inboxDelivery);
        var dagJoin = await DurableDagTerminalNotice.CreateAsync(eventStore, aggregate, decision, cancellationToken).ConfigureAwait(false);
        if (dagJoin is not null) batch = batch with { OutboxRecords = batch.OutboxRecords.Concat([dagJoin]).ToArray() };
        var appendResult = await eventStore
            .AppendAsync(
                batch,
                cancellationToken)
            .ConfigureAwait(false);

        if (appendResult.IsFailure)
        {
            // A scoped reservation belongs to the durable occurrence. Another worker may
            // already have committed this very ticket, or may recover it after host loss.
            if (!decision.HasScopedReservation)
                await resourcePoolCommitEffects.RollBackAcquiresAsync(decision.Events, cancellationToken).ConfigureAwait(false);
            return new DurableCommandResult(
                DurableCommandOutcome.Conflict,
                appendResult.Error.Message,
                aggregate.StreamVersion);
        }

        await resourcePoolCommitEffects.ReleaseCommittedTicketsAsync(decision.Events, decision.Checkpoint, cancellationToken)
            .ConfigureAwait(false);
        var version = appendResult.Value.NewVersion;
        if (decision.Checkpoint is { } checkpoint && decision.Events.Count > 0)
        {
            var settled = await resourcePoolCommitEffects.SettleStoppedReservationsAsync(
                eventStore, checkpoint, decision.Events[^1].OccurredAt, cancellationToken).ConfigureAwait(false);
            if (settled != checkpoint.StreamVersion)
            {
                if (!DurableScopedReservationRecovery.IsTerminal(checkpoint))
                    return new DurableCommandResult(DurableCommandOutcome.Conflict, "Reload compensated lease occurrence.", settled);
                version = settled;
            }
        }

        return new DurableCommandResult(
            DurableCommandOutcome.Committed,
            null,
            version,
            decision.EvictAfterCommit);
    }

    private async Task<DurableCommandResult> CommitNoMutationAsync(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        DurableDecision decision,
        DurableInboxDelivery? inboxDelivery,
        CancellationToken cancellationToken)
    {
        if (inboxDelivery is { Match: not null })
        {
            return new DurableCommandResult(
                DurableCommandOutcome.NoOp,
                "The pending event lost the serialized wait/timer race and remains available.",
                expectedVersion);
        }

        if (inboxDelivery is { } poisonedDelivery)
        {
            return await CommitInboxOnlyAsync(
                instanceId,
                expectedVersion,
                poisonedDelivery,
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
        DurableInboxDelivery delivery,
        InboxRecordState state,
        DurableCommandOutcome successOutcome,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var appendResult = await eventStore
            .AppendAsync(
                commitMaterializer.CreateInboxOnlyBatch(instanceId, expectedVersion, delivery, state),
                cancellationToken)
            .ConfigureAwait(false);

        return appendResult.Match(
            success => new DurableCommandResult(successOutcome, successMessage, success.NewVersion),
            error => new DurableCommandResult(DurableCommandOutcome.Conflict, error.Message, expectedVersion));
    }
}

internal sealed record DurableInboxDelivery(
    EventId EventId,
    string EnvelopeFingerprint,
    DurableEventEnvelope Envelope)
{
    internal InboxMatchSnapshot? Match { get; init; }

    internal InstanceId? TargetInstanceId { get; init; }
}
