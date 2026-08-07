using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Internal;

namespace OrcaCore.Engine.Durable;

internal sealed class DurableWorkflowEventIngressCore(
    DurableWorkflowRuntime runtime,
    IWorkflowProjectionStore projectionStore,
    IWorkflowInboxStore inboxStore,
    bool driveAfterAcceptance = true)
{
    internal ValueTask<WorkflowEventAcceptanceResult> AcceptAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptCoreAsync(
            inboundEvent,
            DurableInboundEventNormalizer.Normalize(inboundEvent),
            cancellationToken);

    internal ValueTask<WorkflowEventAcceptanceResult> AcceptAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        AcceptCoreAsync(
            inboundEvent,
            DurableInboundEventNormalizer.Normalize(inboundEvent),
            cancellationToken);

    private async ValueTask<WorkflowEventAcceptanceResult> AcceptCoreAsync(
        WorkflowInboundEvent inboundEvent,
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);
        cancellationToken.ThrowIfCancellationRequested();
        var prior = await inboxStore.GetByEventIdAsync(inboundEvent.EventId, cancellationToken)
            .ConfigureAwait(false);
        if (prior.HasValue)
        {
            var classified = ClassifyExisting(prior.Value, normalized.Fingerprint);
            if (classified is WorkflowEventAcceptanceResult.Duplicate)
            {
                await TryDeliverPreviouslyAcceptedAsync(
                    inboundEvent,
                    normalized.Envelope.CorrelationId,
                    cancellationToken).ConfigureAwait(false);
            }

            return classified;
        }

        return inboundEvent.Route switch
        {
            WorkflowEventRoute.Direct direct => await AcceptDirectAsync(
                direct.InstanceId, inboundEvent.EventContract, normalized, cancellationToken).ConfigureAwait(false),
            WorkflowEventRoute.Correlation correlation => await AcceptByCorrelationAsync(
                correlation.DefinitionId, inboundEvent.EventContract, normalized, cancellationToken).ConfigureAwait(false),
            WorkflowEventRoute.DefinitionFanout => throw new InvalidOperationException(
                "Definition-fanout acceptance requires the task 7.30 atomic provider snapshot implementation."),
            _ => throw new InvalidOperationException(
                "Start-or-deliver acceptance requires the task 7.31 pending start-intent implementation.")
        };
    }

    private async ValueTask<WorkflowEventAcceptanceResult> AcceptByCorrelationAsync(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        var matches = await projectionStore.FindActiveWaitsAsync(
            definitionId,
            eventContract.EventName,
            eventContract.Version,
            normalized.Envelope.CorrelationId,
            cancellationToken).ConfigureAwait(false);
        if (matches.Count == 0)
        {
            return await PersistAcceptanceAsync(normalized, cancellationToken).ConfigureAwait(false);
        }

        if (matches.Count > 1)
        {
            throw DurableApplicationContractFactory.AmbiguousWait(
                definitionId, eventContract, normalized.Envelope.CorrelationId);
        }

        return await AcceptAndTryDeliverAsync(
            matches[0], eventContract, normalized, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowEventAcceptanceResult> AcceptDirectAsync(
        InstanceId instanceId,
        WorkflowEventContract eventContract,
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        var projected = await projectionStore.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!projected.HasValue)
        {
            return new WorkflowEventAcceptanceResult.Rejected(
                new WorkflowEventAcceptanceRejection.DirectInstanceNotFound());
        }

        if (IsTerminal(projected.Value.Status))
        {
            return new WorkflowEventAcceptanceResult.Rejected(
                new WorkflowEventAcceptanceRejection.DirectInstanceTerminal());
        }

        return await AcceptAndTryDeliverAsync(
            projected.Value, eventContract, normalized, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowEventAcceptanceResult> AcceptAndTryDeliverAsync(
        WorkflowProjectionSnapshot snapshot,
        WorkflowEventContract eventContract,
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        var ownership = await PersistAcceptanceAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (ownership is not WorkflowEventAcceptanceResult.Accepted &&
            ownership is not WorkflowEventAcceptanceResult.Duplicate)
        {
            return ownership;
        }

        if (!snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, eventContract.EventName.Value, StringComparison.Ordinal) &&
                wait.EventContractVersion == eventContract.Version.Value &&
                wait.CorrelationId.Equals(normalized.Envelope.CorrelationId)))
        {
            return ownership;
        }

        await TryDeliverPendingAsync(
            snapshot,
            eventContract,
            normalized.Envelope.CorrelationId,
            cancellationToken).ConfigureAwait(false);
        return ownership;
    }

    private async ValueTask TryDeliverPendingAsync(
        WorkflowProjectionSnapshot snapshot,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var match = await inboxStore.GetMatchSnapshotAsync(
            new InboxMatchRequest(
                snapshot.InstanceId,
                snapshot.DefinitionId,
                eventContract.EventName,
                eventContract.Version,
                correlationId),
            cancellationToken).ConfigureAwait(false);
        if (match.PendingEvent?.Envelope is not { } pendingEnvelope)
        {
            return;
        }

        await runtime.RaiseFacadeEventAsync(
            snapshot.InstanceId,
            pendingEnvelope,
            cancellationToken,
            driveAfterAcceptance,
            match.PendingEvent.EnvelopeFingerprint,
            match).ConfigureAwait(false);
    }

    private async ValueTask TryDeliverPreviouslyAcceptedAsync(
        WorkflowInboundEvent inboundEvent,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        WorkflowProjectionSnapshot? target = null;
        switch (inboundEvent.Route)
        {
            case WorkflowEventRoute.Direct direct:
            {
                var projected = await projectionStore.GetAsync(direct.InstanceId, cancellationToken)
                    .ConfigureAwait(false);
                if (projected.HasValue && !IsTerminal(projected.Value.Status))
                {
                    target = projected.Value;
                }

                break;
            }
            case WorkflowEventRoute.Correlation correlation:
            {
                var matches = await projectionStore.FindActiveWaitsAsync(
                    correlation.DefinitionId,
                    inboundEvent.EventContract.EventName,
                    inboundEvent.EventContract.Version,
                    correlationId,
                    cancellationToken).ConfigureAwait(false);
                if (matches.Count == 1)
                {
                    target = matches[0];
                }

                break;
            }
        }

        if (target is not null && target.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, inboundEvent.EventContract.EventName.Value, StringComparison.Ordinal) &&
                wait.EventContractVersion == inboundEvent.EventContract.Version.Value &&
                wait.CorrelationId.Equals(correlationId)))
        {
            await TryDeliverPendingAsync(
                target,
                inboundEvent.EventContract,
                correlationId,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<WorkflowEventAcceptanceResult> PersistAcceptanceAsync(
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        var commit = await inboxStore.AcceptAsync(
            new InboxAcceptance(
                normalized.Envelope,
                normalized.Fingerprint,
                runtime.UtcNow),
            cancellationToken).ConfigureAwait(false);
        return commit.Disposition switch
        {
            InboxAcceptanceCommitDisposition.Accepted => new WorkflowEventAcceptanceResult.Accepted(),
            InboxAcceptanceCommitDisposition.Duplicate => new WorkflowEventAcceptanceResult.Duplicate(),
            InboxAcceptanceCommitDisposition.Conflict =>
                new WorkflowEventAcceptanceResult.Rejected(new WorkflowEventAcceptanceRejection.EventConflict()),
            InboxAcceptanceCommitDisposition.DirectInstanceNotFound =>
                new WorkflowEventAcceptanceResult.Rejected(
                    new WorkflowEventAcceptanceRejection.DirectInstanceNotFound()),
            InboxAcceptanceCommitDisposition.DirectInstanceTerminal =>
                new WorkflowEventAcceptanceResult.Rejected(
                    new WorkflowEventAcceptanceRejection.DirectInstanceTerminal()),
            _ => throw new ArgumentOutOfRangeException(nameof(commit), commit.Disposition, "Unknown inbox acceptance result.")
        };
    }

    private static WorkflowEventAcceptanceResult ClassifyExisting(InboxRecord existing, string fingerprint) =>
        string.Equals(existing.EnvelopeFingerprint, fingerprint, StringComparison.Ordinal)
            ? new WorkflowEventAcceptanceResult.Duplicate()
            : new WorkflowEventAcceptanceResult.Rejected(new WorkflowEventAcceptanceRejection.EventConflict());

    private static bool IsTerminal(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Failed or
            WorkflowInstanceStatus.TimedOut or WorkflowInstanceStatus.Cancelled or
            WorkflowInstanceStatus.Terminated;
}
