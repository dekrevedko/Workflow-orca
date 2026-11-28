using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Internal;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<DurableSegmentResult?> ReconcileLeaseTicketsAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        var changed = false;
        for (var index = 0; index < ownedObligations.Count; index++)
        {
            var obligation = ownedObligations[index];
            if (obligation.Kind != DurableOwnedObligationKind.Resource ||
                obligation.HolderKey is null ||
                obligation.LeasePhase is not (
                    nameof(DurableLeaseObligationPhase.Held) or
                    nameof(DurableLeaseObligationPhase.ReviewMarked) or
                    nameof(DurableLeaseObligationPhase.AmbiguousHeld) or
                    nameof(DurableLeaseObligationPhase.Quarantined)))
            {
                continue;
            }

            var committedTickets = CommittedLeaseTickets(context.Aggregate, obligation.HolderKey);
            if (committedTickets.Length == 0)
            {
                // A normal release event may have won while an older envelope is being
                // re-entered. Its idempotent release instruction owns that reconciliation;
                // missing-ticket integrity applies only while workflow ownership remains.
                continue;
            }

            if (!CoversLeaseRequest(obligation, committedTickets))
            {
                return await ParkAsync(context, DurableParkReason.Poison,
                    $"Scoped grant for '{obligation.HolderKey}' is missing its committed tickets.",
                    cancellationToken).ConfigureAwait(false);
            }

            var actual = await context.Processor.GetResourceTicketsAsync(
                context.InstanceId, obligation.HolderKey, cancellationToken).ConfigureAwait(false);
            var expected = obligation.LeaseTickets.Count == 0
                ? committedTickets.Select(ToPersistedTicket).ToArray()
                : obligation.LeaseTickets;
            var missing = MissingExactPools(expected, actual);
            if (missing.Count > 0)
            {
                var token = LeaseProtectionToken.Parse(
                    obligation.ProtectionToken ??
                    throw new InvalidOperationException("A live lease has no protection token."));
                // Only a detached obligation can adopt a stop-confirmation receipt.
                // An active owner must still hold its exact tickets before dispatch.
                if (obligation.LeasePhase == nameof(DurableLeaseObligationPhase.Quarantined))
                {
                    var released = await context.Processor.GetResourceReleaseEvidenceAsync(
                        token, cancellationToken).ConfigureAwait(false);
                    if (released.HasValue && DurableResourceLeaseRecovery.CanCloseQuarantinedLease(
                        context.InstanceId, obligation, committedTickets, released.Value))
                    {
                        ownedObligations[index] = obligation with
                        {
                            LeasePhase = nameof(DurableLeaseObligationPhase.Released),
                            AcceptedConfirmationId = released.Value.ConfirmationId?.Value
                        };
                        changed = true;
                        continue;
                    }
                }

                var failure = global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.LeaseLost(token, missing);
                ownedObligations[index] = obligation with
                {
                    LeasePhase = nameof(DurableLeaseObligationPhase.LeaseLost),
                    LeaseTickets = expected
                };
                var committed = await context.Processor.ProcessAsync(
                    new DurableStepFailedCommand(
                        CommandId.New(),
                        context.InstanceId,
                        context.TimeProvider.GetUtcNow(),
                        "lease-reconciliation",
                        $"{failure.Code}: {failure.Message} Expected=[{Describe(expected)}] Actual=[{Describe(actual)}] " +
                        $"MissingPools=[{string.Join(",", failure.MissingPools.Select(pool => pool.Value))}]",
                        BuildEnvelope(context, execution, state, ownedObligations))
                    {
                        ExpectedStreamVersion = currentVersion,
                        PreserveOwnership = true
                    },
                    cancellationToken).ConfigureAwait(false);
                return committed.Outcome == DurableCommandOutcome.Committed
                    ? DurableSegmentResult.Terminal
                    : Conflict(committed);
            }

            // Governance can update review metadata. The workflow grant defines which
            // ticket identities belong in this occurrence, including on an old empty checkpoint.
            var persistedTickets = actual.Where(ticket => expected.Any(fact =>
                    string.Equals(fact.TicketId, ticket.TicketId.ToString("N"), StringComparison.Ordinal)))
                .Select(ToPersistedTicket)
                .ToArray();
            var phase = obligation.LeasePhase;
            if (string.Equals(phase, nameof(DurableLeaseObligationPhase.Held), StringComparison.Ordinal) &&
                persistedTickets.Any(ticket => ticket.ReviewMarked))
            {
                phase = nameof(DurableLeaseObligationPhase.ReviewMarked);
            }

            if (!string.Equals(phase, obligation.LeasePhase, StringComparison.Ordinal) ||
                !TicketFactsEqual(obligation.LeaseTickets, persistedTickets))
            {
                ownedObligations[index] = obligation with
                {
                    LeasePhase = phase,
                    LeaseTickets = persistedTickets
                };
                changed = true;
            }
        }

        if (!changed)
        {
            return null;
        }

        var checkpoint = await context.Processor.ProcessAsync(
            new DurableYieldCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                "lease-reconciliation",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken).ConfigureAwait(false);
        return checkpoint.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.PolicyBoundary
            : Conflict(checkpoint);
    }

    private static ResourcePoolTicket[] CommittedLeaseTickets(DurableWorkflowAggregate aggregate, string holderKey) =>
        aggregate.ResourcePoolState.ActiveTickets
            .Where(ticket => ticket.HolderInstanceId.Equals(aggregate.InstanceId) &&
                string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
            .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
            .ThenBy(ticket => ticket.ProviderGeneration)
            .ToArray();

    private static bool CoversLeaseRequest(DurableOwnedObligationState lease, ResourcePoolTicket[] tickets) =>
        tickets.Length != 0 &&
        tickets.All(ticket => lease.LeaseRequirements.Any(requirement =>
            string.Equals(requirement.PoolName, ticket.PoolName, StringComparison.Ordinal))) &&
        MissingRequiredPools(lease.LeaseRequirements, tickets).Count == 0;

    private static DurableOwnedObligationState ActivateLease(
        DurableOwnedObligationState lease, ResourcePoolTicket[] tickets) => lease with
        {
            LeasePhase = nameof(DurableLeaseObligationPhase.Held),
            LeaseTickets = tickets.Select(ToPersistedTicket).ToArray()
        };

    private static IReadOnlyList<ResourcePoolName> MissingRequiredPools(
        IReadOnlyList<ResourcePoolRequirement> requirements,
        IReadOnlyList<ResourcePoolTicket> actual) =>
        requirements
            .Where(requirement => actual
                .Where(ticket => string.Equals(
                    ticket.PoolName,
                    requirement.PoolName,
                    StringComparison.Ordinal))
                .Sum(ticket => ticket.Count) != requirement.Count)
            .Select(requirement => ResourcePoolName.Create(requirement.PoolName))
            .Distinct()
            .OrderBy(pool => pool.Value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ResourcePoolName> MissingExactPools(
        IReadOnlyList<DurableLeaseTicketState> expected,
        IReadOnlyList<ResourcePoolTicket> actual) =>
        expected
            .Where(ticket => !actual.Any(candidate =>
                string.Equals(candidate.TicketId.ToString("N"), ticket.TicketId, StringComparison.Ordinal) &&
                string.Equals(candidate.PoolName, ticket.PoolName, StringComparison.Ordinal) &&
                candidate.Count == ticket.Units &&
                candidate.ProviderGeneration == ticket.ProviderGeneration))
            .Select(ticket => ResourcePoolName.Create(ticket.PoolName))
            .Distinct()
            .OrderBy(pool => pool.Value, StringComparer.Ordinal)
            .ToArray();

    private static DurableLeaseTicketState ToPersistedTicket(ResourcePoolTicket ticket) =>
        new()
        {
            TicketId = ticket.TicketId.ToString("N"),
            PoolName = ticket.PoolName,
            Units = ticket.Count,
            ProviderGeneration = ticket.ProviderGeneration,
            ReviewDeadline = ticket.ReviewDeadline,
            ReviewMarked = ticket.ReviewMarked
        };

    private static bool TicketFactsEqual(
        IReadOnlyList<DurableLeaseTicketState> left,
        IReadOnlyList<DurableLeaseTicketState> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(pair =>
            pair.First.TicketId == pair.Second.TicketId &&
            pair.First.PoolName == pair.Second.PoolName &&
            pair.First.Units == pair.Second.Units &&
            pair.First.ProviderGeneration == pair.Second.ProviderGeneration &&
            pair.First.ReviewDeadline == pair.Second.ReviewDeadline &&
            pair.First.ReviewMarked == pair.Second.ReviewMarked);

    private static string Describe(IEnumerable<DurableLeaseTicketState> tickets) =>
        string.Join(
            ";",
            tickets.Select(ticket =>
                $"{ticket.TicketId}/{ticket.PoolName}/{ticket.Units}/{ticket.ProviderGeneration}"));

    private static string Describe(IEnumerable<ResourcePoolTicket> tickets) =>
        string.Join(
            ";",
            tickets.Select(ticket =>
                $"{ticket.TicketId:N}/{ticket.PoolName}/{ticket.Count}/{ticket.ProviderGeneration}"));
}
