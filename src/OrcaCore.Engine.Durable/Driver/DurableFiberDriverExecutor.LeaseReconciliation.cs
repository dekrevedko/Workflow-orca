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

            var actual = await context.Processor.GetResourceTicketsAsync(
                context.InstanceId,
                obligation.HolderKey,
                cancellationToken).ConfigureAwait(false);
            var committedTickets = context.Aggregate.ResourcePoolState.ActiveTickets
                .Where(ticket => string.Equals(
                    ticket.HolderKey,
                    obligation.HolderKey,
                    StringComparison.Ordinal))
                .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
                .ThenBy(ticket => ticket.ProviderGeneration)
                .ToArray();
            if (committedTickets.Length == 0)
            {
                // A normal release event may have won while an older envelope is being
                // re-entered. Its idempotent release instruction owns that reconciliation;
                // missing-ticket integrity applies only while workflow ownership remains.
                continue;
            }

            var expected = obligation.LeaseTickets.Count == 0
                ? committedTickets.Select(ToPersistedTicket).ToArray()
                : obligation.LeaseTickets;
            var missing = expected.Count == 0
                ? MissingRequiredPools(obligation.LeaseRequirements, actual)
                : MissingExactPools(expected, actual);
            if (missing.Count > 0)
            {
                var token = LeaseProtectionToken.Parse(
                    obligation.ProtectionToken ??
                    throw new InvalidOperationException("A live lease has no protection token."));
                var released = await context.Processor.GetResourceReleaseEvidenceAsync(
                    token,
                    cancellationToken).ConfigureAwait(false);
                if (released.HasValue)
                {
                    ownedObligations[index] = obligation with
                    {
                        LeasePhase = nameof(DurableLeaseObligationPhase.Released),
                        AcceptedConfirmationId = released.Value.ConfirmationId?.Value
                    };
                    changed = true;
                    continue;
                }

                var failure = global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.LeaseLost(token, missing);
                ownedObligations[index] = obligation with
                {
                    LeasePhase = nameof(DurableLeaseObligationPhase.LeaseLost)
                };
                var committed = await context.Processor.ProcessAsync(
                    new DurableStepFailedCommand(
                        CommandId.New(),
                        context.InstanceId,
                        context.TimeProvider.GetUtcNow(),
                        "lease-reconciliation",
                        $"{failure.Code}: {failure.Message} Expected=[{Describe(expected)}] Actual=[{Describe(actual)}]",
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

            var persistedTickets = actual.Select(ToPersistedTicket).ToArray();
            var phase = obligation.LeasePhase;
            if (string.Equals(phase, nameof(DurableLeaseObligationPhase.Held), StringComparison.Ordinal) &&
                actual.Any(ticket => ticket.ReviewMarked))
            {
                phase = nameof(DurableLeaseObligationPhase.ReviewMarked);
            }

            if (!string.Equals(phase, obligation.LeasePhase, StringComparison.Ordinal) ||
                !TicketFactsEqual(expected, persistedTickets))
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
