using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed class DurableResourceLeaseRecovery(
    DurableCommandProcessor processor,
    IWorkflowProjectionStore projections,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async ValueTask<ProtectedWorkStopConfirmationStatus> ConfirmProtectedWorkStoppedAsync(
        LeaseProtectionToken protectionToken,
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        ArgumentNullException.ThrowIfNull(confirmationId);

        // Publishing the workflow checkpoint can lose a CAS after our release already committed.
        var releasedByThisCall = false;
        for (var retry = 0; retry < 8; retry++)
        {
            var retainedBinding = await processor.GetResourceConfirmationBindingAsync(
                confirmationId,
                cancellationToken).ConfigureAwait(false);
            if (retainedBinding.HasValue &&
                !retainedBinding.Value.Equals(protectionToken))
            {
                return ProtectedWorkStopConfirmationStatus.ConfirmationConflict;
            }

            var retainedRelease = await processor.GetResourceReleaseEvidenceAsync(
                protectionToken,
                cancellationToken).ConfigureAwait(false);
            var leases = await LoadLeaseRecordsAsync(cancellationToken).ConfigureAwait(false);
            if (!retainedBinding.HasValue && await HasForeignWorkflowBindingAsync(
                    leases, protectionToken, confirmationId, cancellationToken).ConfigureAwait(false))
            {
                return ProtectedWorkStopConfirmationStatus.ConfirmationConflict;
            }

            var target = leases.SingleOrDefault(record =>
                string.Equals(
                    record.Obligation.ProtectionToken,
                    protectionToken.Value,
                    StringComparison.Ordinal));
            if (retainedRelease.HasValue &&
                retainedRelease.Value.ConfirmationId is not null)
            {
                // An already released tombstone may carry a losing confirmation ID from an older writer.
                // Closing a still-detached lease requires evidence for its complete original grant.
                if (target is not null &&
                    ((target.Obligation.LeasePhase == nameof(DurableLeaseObligationPhase.Released) &&
                      target.Obligation.AcceptedConfirmationId != retainedRelease.Value.ConfirmationId.Value) ||
                     CanCloseQuarantinedLease(target.InstanceId, target.Obligation,
                         target.ActiveTickets, retainedRelease.Value)))
                {
                    var closed = await CommitReleasedTombstoneAsync(
                        target,
                        retainedRelease.Value.ConfirmationId.Value,
                        cancellationToken).ConfigureAwait(false);
                    if (closed == DurableCommandOutcome.Conflict)
                    {
                        continue;
                    }
                }

                return releasedByThisCall
                    ? ProtectedWorkStopConfirmationStatus.Released
                    : ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
            }

            if (retainedRelease.HasValue &&
                retainedRelease.Value.ConfirmationId is null)
            {
                return ProtectedWorkStopConfirmationStatus.TokenNotFound;
            }

            if (target is null)
            {
                return ProtectedWorkStopConfirmationStatus.TokenNotFound;
            }

            if (target.Obligation.AcceptedConfirmationId is not null)
            {
                return releasedByThisCall && target.Obligation.AcceptedConfirmationId == confirmationId.Value
                    ? ProtectedWorkStopConfirmationStatus.Released
                    : ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
            }

            if (target.Obligation.LeasePhase ==
                nameof(DurableLeaseObligationPhase.Released))
            {
                return ProtectedWorkStopConfirmationStatus.TokenNotFound;
            }

            if (target.Obligation.LeasePhase !=
                nameof(DurableLeaseObligationPhase.Quarantined))
            {
                return ProtectedWorkStopConfirmationStatus.NotConfirmable;
            }

            var providerResult = await processor.ConfirmAndReleaseResourceHolderAsync(
                new ResourcePoolStopConfirmationRequest(
                    target.InstanceId,
                    target.Obligation.HolderKey ??
                        throw new InvalidOperationException(
                            "A quarantined lease has no holder key."),
                    protectionToken,
                    confirmationId,
                    timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
            switch (providerResult)
            {
                case ResourcePoolStopConfirmationStatus.ConfirmationConflict:
                    return ProtectedWorkStopConfirmationStatus.ConfirmationConflict;
                case ResourcePoolStopConfirmationStatus.TokenNotFound:
                    return ProtectedWorkStopConfirmationStatus.TokenNotFound;
                case ResourcePoolStopConfirmationStatus.AlreadyConfirmed:
                    break;
                case ResourcePoolStopConfirmationStatus.Released:
                    releasedByThisCall = true;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown provider stop-confirmation result '{providerResult}'.");
            }

            var winner = await processor.GetResourceReleaseEvidenceAsync(
                protectionToken, cancellationToken).ConfigureAwait(false);
            if (!winner.HasValue || winner.Value.ConfirmationId is null)
            {
                continue;
            }

            // Both provider outcomes describe governance. Only a receipt for the original
            // grant authorizes closing this workflow's quarantined obligation.
            if (!CanCloseQuarantinedLease(target.InstanceId, target.Obligation, target.ActiveTickets, winner.Value))
            {
                return releasedByThisCall
                    ? ProtectedWorkStopConfirmationStatus.Released
                    : ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
            }

            var commit = await CommitReleasedTombstoneAsync(
                target,
                winner.Value.ConfirmationId.Value,
                cancellationToken).ConfigureAwait(false);
            if (commit == DurableCommandOutcome.Committed)
            {
                return releasedByThisCall
                    ? ProtectedWorkStopConfirmationStatus.Released
                    : ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
            }

            if (commit != DurableCommandOutcome.Conflict)
            {
                return ProtectedWorkStopConfirmationStatus.TokenNotFound;
            }
        }

        throw new InvalidOperationException(
            "Lease stop confirmation could not serialize after repeated conflicts.");
    }

    private async Task<bool> HasForeignWorkflowBindingAsync(
        IReadOnlyList<LeaseRecord> leases,
        LeaseProtectionToken requestedToken,
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken)
    {
        foreach (var record in leases)
        {
            var obligation = record.Obligation;
            if (obligation.AcceptedConfirmationId != confirmationId.Value ||
                obligation.ProtectionToken == requestedToken.Value)
            {
                continue;
            }

            // An older checkpoint may name the losing confirmer. Its token's durable
            // receipt takes precedence; without a receipt the checkpoint is the evidence.
            var receipt = await processor.GetResourceReleaseEvidenceAsync(
                LeaseProtectionToken.Parse(obligation.ProtectionToken!), cancellationToken).ConfigureAwait(false);
            if (!receipt.HasValue || confirmationId.Equals(receipt.Value.ConfirmationId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<DurableCommandOutcome> CommitReleasedTombstoneAsync(
        LeaseRecord target,
        string? confirmationId,
        CancellationToken cancellationToken)
    {
        var obligations = target.Envelope.OwnedObligations.ToArray();
        obligations[target.ObligationIndex] = target.Obligation with
        {
            LeasePhase = nameof(DurableLeaseObligationPhase.Released),
            AcceptedConfirmationId = confirmationId
        };
        var updated = target.Envelope with { OwnedObligations = obligations };
        var commit = await processor.ProcessAsync(
            new DurableLeaseStopConfirmedCommand(
                CommandId.New(),
                target.InstanceId,
                timeProvider.GetUtcNow(),
                target.Obligation.HolderKey ??
                    throw new InvalidOperationException(
                        "A retained lease tombstone has no holder key."),
                updated)
            {
                ExpectedStreamVersion = target.StreamVersion
            },
            cancellationToken).ConfigureAwait(false);
        return commit.Outcome;
    }

    internal async ValueTask<int> ReconcileReleaseGapsAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        var reconciled = 0;
        for (var retry = 0; retry < 8; retry++)
        {
            var conflict = false;
            var candidates = (await LoadLeaseRecordsAsync(cancellationToken).ConfigureAwait(false))
                .Where(record =>
                    record.InstanceId.Equals(instanceId) &&
                    record.Obligation.LeasePhase == nameof(DurableLeaseObligationPhase.Quarantined))
                .ToArray();
            foreach (var candidate in candidates)
            {
                var token = LeaseProtectionToken.Parse(
                    candidate.Obligation.ProtectionToken ??
                    throw new InvalidOperationException(
                        "A release-gap candidate has no protection token."));
                var evidence = await processor.GetResourceReleaseEvidenceAsync(
                    token,
                    cancellationToken).ConfigureAwait(false);
                if (!evidence.HasValue || !CanCloseQuarantinedLease(candidate.InstanceId, candidate.Obligation,
                    candidate.ActiveTickets, evidence.Value))
                {
                    continue;
                }

                var outcome = await CommitReleasedTombstoneAsync(
                    candidate,
                    evidence.Value.ConfirmationId?.Value,
                    cancellationToken).ConfigureAwait(false);
                if (outcome == DurableCommandOutcome.Conflict)
                {
                    conflict = true;
                    break;
                }

                if (outcome == DurableCommandOutcome.Committed)
                {
                    reconciled++;
                }
            }

            if (!conflict)
            {
                return reconciled;
            }
        }

        throw new InvalidOperationException(
            "Lease release-gap reconciliation could not serialize after repeated conflicts.");
    }

    internal static bool CanCloseQuarantinedLease(
        InstanceId instanceId, DurableOwnedObligationState obligation,
        IReadOnlyList<ResourcePoolTicket> workflowTickets, ResourcePoolReleaseEvidence receipt)
    {
        if (obligation.LeasePhase != nameof(DurableLeaseObligationPhase.Quarantined) ||
            !instanceId.Equals(receipt.HolderInstanceId) ||
            !string.Equals(obligation.HolderKey, receipt.HolderKey, StringComparison.Ordinal) ||
            !string.Equals(obligation.ProtectionToken, receipt.ProtectionToken?.Value, StringComparison.Ordinal))
        {
            return false;
        }

        // Older checkpoints retained ticket identities only in the workflow acquisition.
        var expected = obligation.LeaseTickets.Count > 0
            ? obligation.LeaseTickets.Select(t => (t.TicketId, t.PoolName, t.Units, t.ProviderGeneration)).ToHashSet()
            : workflowTickets.Where(t => t.HolderInstanceId.Equals(instanceId) && t.HolderKey == receipt.HolderKey)
                .Select(t => (t.TicketId.ToString("N"), t.PoolName, t.Count, t.ProviderGeneration)).ToHashSet();
        return expected.Count > 0 && expected.Count == receipt.ReleasedTickets.Count &&
            receipt.ReleasedTickets.All(t => t.HolderInstanceId.Equals(instanceId) && t.HolderKey == receipt.HolderKey) &&
            expected.SetEquals(receipt.ReleasedTickets.Select(t =>
                (t.TicketId.ToString("N"), t.PoolName, t.Count, t.ProviderGeneration)));
    }

    private async Task<IReadOnlyList<LeaseRecord>> LoadLeaseRecordsAsync(
        CancellationToken cancellationToken)
    {
        var snapshots = await projections.ListLeaseRecoveryCandidatesAsync(cancellationToken).ConfigureAwait(false);
        var records = new List<LeaseRecord>();
        foreach (var snapshot in snapshots)
        {
            var checkpoint = await processor.EventStore.LoadCheckpointAsync(
                snapshot.InstanceId,
                cancellationToken).ConfigureAwait(false);
            if (!checkpoint.HasValue ||
                checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
            {
                continue;
            }

            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
            for (var index = 0; index < envelope.OwnedObligations.Count; index++)
            {
                var obligation = envelope.OwnedObligations[index];
                if (obligation.Kind == DurableOwnedObligationKind.Resource &&
                    obligation.ProtectionToken is not null)
                {
                    records.Add(new LeaseRecord(
                        snapshot.InstanceId,
                        checkpoint.Value.StreamVersion,
                        envelope,
                        obligation,
                        checkpoint.Value.RuntimeState.ActiveResourceTickets,
                        index));
                }
            }
        }

        return records;
    }

    private sealed record LeaseRecord(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        DurableExecutionEnvelopeV2 Envelope,
        DurableOwnedObligationState Obligation,
        IReadOnlyList<ResourcePoolTicket> ActiveTickets,
        int ObligationIndex);
}
