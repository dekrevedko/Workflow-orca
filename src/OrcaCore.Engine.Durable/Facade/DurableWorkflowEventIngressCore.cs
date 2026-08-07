using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
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
            return ClassifyExisting(prior.Value, normalized.Fingerprint);
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
            throw new InvalidOperationException(
                "Correlation acceptance without an active matching wait requires the task 7.28 route inbox.");
        }

        if (matches.Count > 1)
        {
            throw DurableApplicationContractFactory.AmbiguousWait(
                definitionId, eventContract, normalized.Envelope.CorrelationId);
        }

        return await AcceptToActiveInstanceAsync(
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

        return await AcceptToActiveInstanceAsync(
            projected.Value, eventContract, normalized, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowEventAcceptanceResult> AcceptToActiveInstanceAsync(
        WorkflowProjectionSnapshot snapshot,
        WorkflowEventContract eventContract,
        NormalizedDurableInboundEvent normalized,
        CancellationToken cancellationToken)
    {
        if (!snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, eventContract.EventName.Value, StringComparison.Ordinal) &&
                wait.EventContractVersion == eventContract.Version.Value &&
                wait.CorrelationId.Equals(normalized.Envelope.CorrelationId)))
        {
            throw new InvalidOperationException(
                "Direct acceptance without an active matching wait requires the task 7.28 target inbox.");
        }

        var result = await runtime.RaiseFacadeEventAsync(
            snapshot.InstanceId,
            normalized.Envelope,
            cancellationToken,
            driveAfterAcceptance,
            normalized.Fingerprint).ConfigureAwait(false);
        if (result.Outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.Poisoned)
        {
            return new WorkflowEventAcceptanceResult.Accepted();
        }

        var committed = await inboxStore.GetByEventIdAsync(normalized.Envelope.EventId, cancellationToken)
            .ConfigureAwait(false);
        if (committed.HasValue)
        {
            return ClassifyExisting(committed.Value, normalized.Fingerprint);
        }

        throw new WorkflowConcurrencyException(
            result.Message ?? "Durable event acceptance lost commit authority before ownership was recorded.");
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
