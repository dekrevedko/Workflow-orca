using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class EphemeralRoutingIndex
{
    private readonly ConcurrentDictionary<InstanceId, IReadOnlyList<WaitRoutingKey>> indexedInstanceWaits = [];
    private readonly ConcurrentDictionary<WaitRoutingKey, ConcurrentDictionary<InstanceId, byte>> waitIndex = [];
    private readonly object routingGate = new();
    private readonly Dictionary<(DefinitionId, WorkflowEventContract, CorrelationId), (InstanceId Owner, int Count)> uniqueWaits = [];

    internal Action Reserve(DefinitionId definitionId, InstanceId instanceId, RuntimeWaitRecord wait)
    {
        var key = (definitionId, wait.EventContract, wait.CorrelationId);
        lock (routingGate)
        {
            if (uniqueWaits.TryGetValue(key, out var current) && !current.Owner.Equals(instanceId))
                throw FacadeValueFactory.AmbiguousWait(definitionId, wait.EventContract, wait.CorrelationId);

            // A matched wait remains reserved until its continuation commits.
            // That continuation can register the next occurrence on the same instance.
            uniqueWaits[key] = (instanceId, checked(current.Count + 1));
        }

        return () =>
        {
            lock (routingGate)
            {
                var current = uniqueWaits[key];
                if (current.Count == 1) uniqueWaits.Remove(key);
                else uniqueWaits[key] = (instanceId, current.Count - 1);
            }
        };
    }

    internal InstanceId? FindUniqueOwner(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId)
    {
        lock (routingGate)
            return uniqueWaits.TryGetValue((definitionId, eventContract, correlationId), out var wait) ? wait.Owner : null;
    }

    public IReadOnlyCollection<InstanceId> FindCandidates(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        lock (routingGate)
        {
            var key = WaitRoutingKey.From(envelope);
            return waitIndex.TryGetValue(key, out var indexedInstances) && !indexedInstances.IsEmpty
                ? indexedInstances.Keys.ToArray()
                : [];
        }
    }

    public void IndexSnapshot(EphemeralWorkflowInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (routingGate)
        {
            if (indexedInstanceWaits.TryRemove(snapshot.InstanceId, out var previousKeys))
            {
                foreach (var previousKey in previousKeys)
                {
                    if (waitIndex.TryGetValue(previousKey, out var instances))
                    {
                        instances.TryRemove(snapshot.InstanceId, out _);
                        if (instances.IsEmpty)
                        {
                            waitIndex.TryRemove(previousKey, out _);
                        }
                    }
                }
            }

            var activeKeys = snapshot.ActiveWaits
                .Select(wait => new WaitRoutingKey(
                    wait.EventContract.EventName.Value,
                    wait.EventContract.Version.Value,
                    wait.CorrelationId))
                .Distinct()
                .ToArray();
            if (activeKeys.Length == 0)
            {
                return;
            }

            indexedInstanceWaits[snapshot.InstanceId] = activeKeys;
            foreach (var key in activeKeys)
            {
                waitIndex.GetOrAdd(key, _ => new ConcurrentDictionary<InstanceId, byte>())[snapshot.InstanceId] = 0;
            }
        }
    }

    private readonly record struct WaitRoutingKey(
        string EventName,
        int EventContractVersion,
        CorrelationId CorrelationId)
    {
        internal static WaitRoutingKey From(EventEnvelope envelope)
        {
            return new WaitRoutingKey(
                envelope.EventContract.EventName.Value,
                envelope.EventContract.Version.Value,
                envelope.CorrelationId);
        }
    }
}
