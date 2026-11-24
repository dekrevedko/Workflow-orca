using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableResourcePoolCommitEffects(IResourcePoolStore? resourcePoolStore)
{
    internal async Task RollBackAcquiresAsync(
        IReadOnlyList<DurableWorkflowEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var acquired in events.OfType<WorkflowResourcePoolAcquiredEvent>())
        {
            await RequiredResourcePoolStore()
                .ReleaseAsync(
                    new ResourcePoolReleaseRequest(acquired.InstanceId, acquired.HolderKey, acquired.OccurredAt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal async Task ReleaseCommittedTicketsAsync(
        IReadOnlyList<DurableWorkflowEvent> events,
        CheckpointWrite? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var released in events.OfType<WorkflowResourcePoolReleasedEvent>())
        {
            if (resourcePoolStore is IScopedReservationCloser closer && checkpoint?.ContentType == DurableExecutionEnvelopeV2.ContentType)
            {
                var o = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations
                    .SingleOrDefault(o => o.Kind == DurableOwnedObligationKind.Resource && o.HolderKey == released.HolderKey);
                if (o?.ProtectionToken is { } token)
                {
                    await DurableScopedReservationRecovery.CloseAsync(closer, released.InstanceId, released.HolderKey,
                        LeaseProtectionToken.Parse(token), released.OccurredAt, cancellationToken).ConfigureAwait(false);
                    continue;
                }
            }

            try
            {
                await ReleaseWithRetryAsync(new ResourcePoolReleaseRequest(released.InstanceId, released.HolderKey, released.OccurredAt),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Preserve the legacy unscoped post-commit disposition.
            }
        }
    }

    internal Task<StreamVersion> SettleStoppedReservationsAsync(IWorkflowEventStore events, CheckpointWrite checkpoint,
        DateTimeOffset now, CancellationToken cancellationToken) => resourcePoolStore is IScopedReservationCloser
        ? DurableScopedReservationRecovery.SettleAsync(events, resourcePoolStore, checkpoint, now, cancellationToken)
        : Task.FromResult(checkpoint.StreamVersion);

    private IResourcePoolStore RequiredResourcePoolStore()
    {
        return resourcePoolStore ?? throw new InvalidOperationException(
            "Durable resource-pool acquisition requires a resource-pool-capable provider.");
    }

    private async Task ReleaseWithRetryAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await RequiredResourcePoolStore()
                    .ReleaseAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch when (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
        }
    }
}
