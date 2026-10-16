using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Concurrency;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Owns durable command processing collaborators that must be shared for one host process.
/// </summary>
internal sealed class DurableCommandRuntime
{
    private readonly InstanceLane lanes;
    private readonly ConcurrentDictionary<InstanceId, StepCancellationScope> runningSteps = [];

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

    internal StepCancellationScope EnterStep(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var scope = new StepCancellationScope(this, instanceId, cancellationToken);
        if (!runningSteps.TryAdd(instanceId, scope))
        {
            scope.Dispose();
            throw new InvalidOperationException(
                $"Instance '{instanceId}' already has a running durable business step in this host.");
        }

        return scope;
    }

    internal void RequestStepCancellation(InstanceId instanceId)
    {
        if (runningSteps.TryGetValue(instanceId, out var scope))
        {
            scope.RequestOperatorCancellation();
        }
    }

    internal bool HasRunningStep(InstanceId instanceId) =>
        runningSteps.ContainsKey(instanceId);

    internal sealed class StepCancellationScope : IDisposable
    {
        private readonly CancellationTokenSource cancellation;
        private readonly InstanceId instanceId;
        private readonly DurableCommandRuntime owner;
        private int operatorCancellationRequested;
        private int disposed;

        internal StepCancellationScope(
            DurableCommandRuntime owner,
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            this.owner = owner;
            this.instanceId = instanceId;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        internal CancellationToken Token => cancellation.Token;

        internal bool OperatorCancellationRequested =>
            Volatile.Read(ref operatorCancellationRequested) != 0;

        internal void RequestOperatorCancellation()
        {
            Interlocked.Exchange(ref operatorCancellationRequested, 1);
            _ = cancellation.CancelAsync();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            owner.runningSteps.TryRemove(new KeyValuePair<InstanceId, StepCancellationScope>(instanceId, this));
            cancellation.Dispose();
        }
    }
}
