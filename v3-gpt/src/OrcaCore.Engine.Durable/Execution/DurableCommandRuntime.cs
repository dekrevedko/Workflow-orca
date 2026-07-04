using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Concurrency;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Owns durable command processing collaborators that must be shared for one host process.
/// </summary>
public sealed class DurableCommandRuntime
{
    private readonly InstanceLane lanes;

    /// <summary>
    /// Initializes a new durable command runtime.
    /// </summary>
    /// <param name="eventStore">The durable event store used by command processors.</param>
    /// <param name="resourcePoolStore">The optional durable resource-pool store used by resource commands.</param>
    public DurableCommandRuntime(IWorkflowEventStore eventStore, IResourcePoolStore? resourcePoolStore = null)
        : this(eventStore, resourcePoolStore, onLaneEvicted: null)
    {
    }

    internal DurableCommandRuntime(
        IWorkflowEventStore eventStore,
        IResourcePoolStore? resourcePoolStore,
        Action<InstanceId>? onLaneEvicted)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        lanes = new InstanceLane(onLaneEvicted: onLaneEvicted);
        EventStore = eventStore;
        ResourcePoolStore = resourcePoolStore;
    }

    internal IWorkflowEventStore EventStore { get; }

    internal IResourcePoolStore? ResourcePoolStore { get; }

    internal int ActiveLaneCount => lanes.ActiveLaneCount;

    internal async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        return await lanes.RunAsync(instanceId, operation, cancellationToken).ConfigureAwait(false);
    }
}
