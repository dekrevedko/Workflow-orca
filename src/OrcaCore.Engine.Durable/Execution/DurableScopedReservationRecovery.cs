using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

// Serialized by the governance partition alongside acquire, promotion and release.
internal interface IScopedReservationCloser
{
    Task CloseAsync(ResourcePoolReleaseRequest request, LeaseProtectionToken token, CancellationToken cancellationToken);
}

internal static class DurableScopedReservationRecovery
{
    internal static bool IsTerminal(CheckpointWrite checkpoint) => checkpoint.Status is
        WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Failed or WorkflowInstanceStatus.Cancelled or
        WorkflowInstanceStatus.Terminated or WorkflowInstanceStatus.TimedOut;

    // A stopped owner can no longer activate its pending lease. Governance is authoritative
    // about whether it had already reserved capacity when the owner stopped.
    internal static async Task<StreamVersion> SettleAsync(IWorkflowEventStore events, IResourcePoolStore pools,
        CheckpointWrite checkpoint, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var version = checkpoint.StreamVersion;
        if (pools is not IScopedReservationCloser closer || checkpoint.ContentType != DurableExecutionEnvelopeV2.ContentType)
            return version;

        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        foreach (var obligation in envelope.OwnedObligations)
        {
            if (obligation.ProtectionToken is null || obligation.HolderKey is null ||
                obligation.Kind != DurableOwnedObligationKind.Resource ||
                obligation.LeasePhase is not (nameof(DurableLeaseObligationPhase.Queued) or nameof(DurableLeaseObligationPhase.PendingCommit)))
                continue;
            if (!IsTerminal(checkpoint) && envelope.Fibers.Any(f => f.FiberId == obligation.FiberId &&
                    f.Phase is not (DurableFiberPhase.Completed or DurableFiberPhase.Failed or DurableFiberPhase.Cancelled)))
                continue;

            var token = LeaseProtectionToken.Parse(obligation.ProtectionToken);
            await CloseAsync(closer, checkpoint.InstanceId, obligation.HolderKey, token, now, cancellationToken).ConfigureAwait(false);
            var receipt = (await pools.GetReleaseEvidenceAsync(token, cancellationToken).ConfigureAwait(false)).Value;
            if (!receipt.HolderInstanceId.Equals(checkpoint.InstanceId) || receipt.HolderKey != obligation.HolderKey)
                throw new InvalidOperationException("Lease closure receipt identifies another occurrence.");

            var closed = obligation with
            {
                LeasePhase = receipt.ReleasedTickets.Count == 0
                    ? nameof(DurableLeaseObligationPhase.CancelledBeforeGrant)
                    : nameof(DurableLeaseObligationPhase.Released),
                LeaseTickets = receipt.ReleasedTickets.Select(ticket => new DurableLeaseTicketState
                {
                    TicketId = ticket.TicketId.ToString("N"), PoolName = ticket.PoolName, Units = ticket.Count,
                    ProviderGeneration = ticket.ProviderGeneration, ReviewDeadline = ticket.ReviewDeadline,
                    ReviewMarked = ticket.ReviewMarked
                }).ToArray()
            };
            var updated = envelope with
            {
                OwnedObligations = envelope.OwnedObligations.Select(o => o.ObligationId == closed.ObligationId ? closed : o).ToArray()
            };
            var aggregate = await new DurableAggregateLoader(events).LoadAsync(checkpoint.InstanceId, cancellationToken).ConfigureAwait(false);
            if (aggregate.StreamVersion != version) return aggregate.StreamVersion;
            var tail = await events.LoadTailAsync(new WorkflowStreamId(checkpoint.InstanceId), StreamVersion.Empty, cancellationToken)
                .ConfigureAwait(false);
            var published = tail.OfType<WorkflowResourcePoolReleasedEvent>().Any(e => e.HolderKey == obligation.HolderKey &&
                e.Tickets.Select(t => (t.TicketId, t.ProviderGeneration)).ToHashSet().SetEquals(
                    receipt.ReleasedTickets.Select(t => (t.TicketId, t.ProviderGeneration))));
            var commandId = CommandId.New();
            var decision = aggregate.DecideLeaseStopConfirmed(new DurableLeaseStopConfirmedCommand(
                commandId, checkpoint.InstanceId, now, obligation.HolderKey, updated)
            {
                CompensatedRelease = published ? null : new WorkflowResourcePoolReleasedEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()), InstanceId = checkpoint.InstanceId,
                    CommandId = commandId, CausationId = DurableWorkflowAggregate.ToCausationId(commandId), OccurredAt = now,
                    ParentInstanceId = aggregate.ParentInstanceId, RootInstanceId = aggregate.RootInstanceId ?? checkpoint.InstanceId,
                    HolderKey = obligation.HolderKey, FiberId = new FiberId(obligation.FiberId),
                    ScopeId = obligation.ScopeId is null ? null : new ScopeId(obligation.ScopeId), Tickets = receipt.ReleasedTickets
                }
            });
            var batch = new DurableCommitMaterializer().CreateBatch(checkpoint.InstanceId, version, decision, aggregate, null);
            var result = await events.AppendAsync(batch, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                var current = await new DurableAggregateLoader(events).LoadAsync(checkpoint.InstanceId, cancellationToken).ConfigureAwait(false);
                if (current.StreamVersion != version) return current.StreamVersion;
                throw new InvalidOperationException(result.Error.Message);
            }
            version = result.Value.NewVersion;
            envelope = updated;
        }
        return version;
    }

    internal static async Task CloseAsync(IScopedReservationCloser closer, InstanceId instance, string holder,
        LeaseProtectionToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var request = new ResourcePoolReleaseRequest(instance, holder, now);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await closer.CloseAsync(request, token, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
        }
    }

    internal static async Task SweepAsync(IWorkflowEventStore events, IWorkflowProjectionStore projections,
        IResourcePoolStore pools, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (pools is not IScopedReservationCloser closer) return;
        var configured = await pools.ListPoolsAsync(cancellationToken).ConfigureAwait(false);
        if (configured.Count == 0) return;
        var outstanding = configured.SelectMany(pool => pool.HeldTickets.Select(t => (t.HolderInstanceId, t.HolderKey))
                .Concat(pool.QueuedWaiters.Select(w => (w.HolderInstanceId, w.HolderKey))))
            .ToHashSet();
        // Start from workflows as well as pool owners: a close can precede the first reservation.
        foreach (var workflow in await projections.ListLeaseRecoveryCandidatesAsync(cancellationToken).ConfigureAwait(false))
        {
            var current = await events.LoadCheckpointAsync(workflow.InstanceId, cancellationToken).ConfigureAwait(false);
            if (!current.HasValue || current.Value.ContentType != DurableExecutionEnvelopeV2.ContentType) continue;
            var checkpoint = current.Value;
            await SettleAsync(events, pools, checkpoint, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            foreach (var o in DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations)
            {
                if (o.Kind == DurableOwnedObligationKind.Resource && o.ProtectionToken is not null && o.HolderKey is not null &&
                    o.LeasePhase is (nameof(DurableLeaseObligationPhase.Released) or nameof(DurableLeaseObligationPhase.CancelledBeforeGrant)) &&
                    outstanding.Contains((workflow.InstanceId, o.HolderKey)))
                    await CloseAsync(closer, workflow.InstanceId, o.HolderKey, LeaseProtectionToken.Parse(o.ProtectionToken),
                        clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
