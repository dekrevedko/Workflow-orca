using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Owns durable command processing collaborators that must be shared for one host process.
/// </summary>
public sealed class DurableCommandRuntime
{
    private readonly DurableInstanceCommandLane lanes = new();

    /// <summary>
    /// Initializes a new durable command runtime.
    /// </summary>
    /// <param name="eventStore">The durable event store used by command processors.</param>
    /// <param name="resourcePoolStore">The optional durable resource-pool store used by resource commands.</param>
    public DurableCommandRuntime(IWorkflowEventStore eventStore, IResourcePoolStore? resourcePoolStore = null)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        EventStore = eventStore;
        ResourcePoolStore = resourcePoolStore;
    }

    internal IWorkflowEventStore EventStore { get; }

    internal IResourcePoolStore? ResourcePoolStore { get; }

    internal int ActiveLaneCount => lanes.ActiveLaneCount;

    internal Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        return lanes.RunAsync(instanceId, operation, cancellationToken);
    }
}
