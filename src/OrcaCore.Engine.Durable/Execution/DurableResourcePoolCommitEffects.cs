using OrcaCore.Abstractions.Durable;
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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var released in events.OfType<WorkflowResourcePoolReleasedEvent>())
        {
            await ReleaseWithRetryAsync(
                new ResourcePoolReleaseRequest(released.InstanceId, released.HolderKey, released.OccurredAt),
                cancellationToken)
                .ConfigureAwait(false);
        }
    }

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
