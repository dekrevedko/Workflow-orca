using OrcaCore.Abstractions.Durable;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed class DurableResourceLeaseDiagnostics(
    DurableCommandProcessor processor,
    IWorkflowProjectionStore projections)
{
    public async IAsyncEnumerable<DurableResourceLeaseObligationSnapshot> EnumerateOutstandingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var materialized = new List<DurableResourceLeaseObligationSnapshot>();
        var snapshots = await projections.ListLeaseRecoveryCandidatesAsync(cancellationToken).ConfigureAwait(false);
        var pools = await processor.ListResourcePoolsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var workflow in snapshots.OrderBy(snapshot => snapshot.InstanceId.ToString(), StringComparer.Ordinal))
        {
            var checkpoint = await processor.EventStore.LoadCheckpointAsync(
                workflow.InstanceId,
                cancellationToken).ConfigureAwait(false);
            if (!checkpoint.HasValue ||
                checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
            {
                continue;
            }

            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
            foreach (var obligation in envelope.OwnedObligations
                         .Where(candidate =>
                             candidate.Kind == DurableOwnedObligationKind.Resource &&
                             candidate.ProtectionToken is not null &&
                             candidate.LeasePhase is not (
                                 nameof(DurableLeaseObligationPhase.Released) or
                                 nameof(DurableLeaseObligationPhase.CancelledBeforeGrant)))
                         .OrderBy(candidate => candidate.RegistrationSequence))
            {
                materialized.Add(Materialize(workflow, envelope, obligation, pools));
            }
        }

        OrcaCoreDurableDiagnostics.RefreshLeaseObligations(materialized);
        foreach (var snapshot in materialized)
        {
            yield return snapshot;
        }
    }

    public async ValueTask<DurableResourceLeaseObligationSnapshot?> GetAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        var snapshots = await projections.ListLeaseRecoveryCandidatesAsync(cancellationToken).ConfigureAwait(false);
        var pools = await processor.ListResourcePoolsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var workflow in snapshots.OrderBy(
                     snapshot => snapshot.InstanceId.ToString(),
                     StringComparer.Ordinal))
        {
            var checkpoint = await processor.EventStore.LoadCheckpointAsync(
                workflow.InstanceId,
                cancellationToken).ConfigureAwait(false);
            if (!checkpoint.HasValue ||
                checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
            {
                continue;
            }

            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
            var obligation = envelope.OwnedObligations.SingleOrDefault(candidate =>
                candidate.Kind == DurableOwnedObligationKind.Resource &&
                candidate.LeasePhase != nameof(DurableLeaseObligationPhase.CancelledBeforeGrant) &&
                string.Equals(
                    candidate.ProtectionToken,
                    protectionToken.Value,
                    StringComparison.Ordinal));
            if (obligation is not null)
            {
                return Materialize(workflow, envelope, obligation, pools);
            }
        }

        return null;
    }

    private static DurableResourceLeaseObligationSnapshot Materialize(
        ProjectionWorkflowInstanceSnapshot workflow,
        DurableExecutionEnvelopeV2 envelope,
        DurableOwnedObligationState obligation,
        IReadOnlyList<ResourcePoolSnapshot> pools)
    {
        var providerTickets = pools
            .SelectMany(pool => pool.HeldTickets)
            .Where(ticket =>
                ticket.HolderInstanceId.Equals(workflow.InstanceId) &&
                string.Equals(ticket.HolderKey, obligation.HolderKey, StringComparison.Ordinal))
            .ToDictionary(ticket => ticket.TicketId.ToString("N"), StringComparer.Ordinal);
        var tickets = obligation.LeaseTickets
            .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
            .ThenBy(ticket => ticket.ProviderGeneration)
            .Select(ticket =>
            {
                providerTickets.TryGetValue(ticket.TicketId, out var current);
                return new DurableResourceLeaseTicketSnapshot(
                    ticket.TicketId,
                    ResourcePoolName.Create(ticket.PoolName),
                    ticket.Units,
                    ticket.ProviderGeneration,
                    ticket.ReviewDeadline ?? DateTimeOffset.MaxValue,
                    ticket.ReviewMarked || current?.ReviewMarked == true);
            })
            .ToArray();
        var status = Enum.TryParse<DurableResourceLeaseObligationStatus>(
            obligation.LeasePhase,
            out var parsed)
            ? parsed
            : DurableResourceLeaseObligationStatus.Queued;

        return new DurableResourceLeaseObligationSnapshot(
            obligation.ObligationId,
            workflow.InstanceId,
            envelope.PlanBinding.DefinitionId,
            envelope.PlanBinding.DefinitionVersion,
            checked((int)envelope.ContinueAsNewGeneration),
            obligation.FiberId,
            obligation.ScopeId ?? "root",
            FailureProvenance.LocationFromCompilerPath(obligation.AuthoredPath),
            LeaseProtectionToken.Parse(obligation.ProtectionToken!),
            status,
            tickets,
            status == DurableResourceLeaseObligationStatus.Quarantined
                ? workflow.UpdatedAt
                : null,
            obligation.AcceptedConfirmationId is null
                ? null
                : StopConfirmationId.Create(obligation.AcceptedConfirmationId));
    }
}
