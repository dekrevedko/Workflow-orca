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
            if (leases.Any(record =>
                    string.Equals(
                        record.Obligation.AcceptedConfirmationId,
                        confirmationId.Value,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        record.Obligation.ProtectionToken,
                        protectionToken.Value,
                        StringComparison.Ordinal)))
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
                if (target is not null &&
                    (target.Obligation.AcceptedConfirmationId is null ||
                     target.Obligation.LeasePhase != nameof(DurableLeaseObligationPhase.Released)))
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

                return ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
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
                return ProtectedWorkStopConfirmationStatus.AlreadyConfirmed;
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
                case ResourcePoolStopConfirmationStatus.Released:
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown provider stop-confirmation result '{providerResult}'.");
            }

            var commit = await CommitReleasedTombstoneAsync(
                target,
                confirmationId.Value,
                cancellationToken).ConfigureAwait(false);
            if (commit == DurableCommandOutcome.Committed)
            {
                return providerResult == ResourcePoolStopConfirmationStatus.Released
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
                    record.Obligation.LeasePhase is not (
                        nameof(DurableLeaseObligationPhase.Released) or
                        nameof(DurableLeaseObligationPhase.CancelledBeforeGrant) or
                        nameof(DurableLeaseObligationPhase.LeaseLost)))
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
                if (!evidence.HasValue)
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
        int ObligationIndex);
}
