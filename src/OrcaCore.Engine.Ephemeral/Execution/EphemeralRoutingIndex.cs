using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class EphemeralRoutingIndex
{
    private readonly ConcurrentDictionary<InstanceId, IReadOnlyList<WaitRoutingKey>> indexedInstanceWaits = [];
    private readonly ConcurrentDictionary<WaitRoutingKey, ConcurrentDictionary<InstanceId, byte>> waitIndex = [];
    private readonly object routingGate = new();

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
                .Select(wait => new WaitRoutingKey(wait.EventName, wait.CorrelationId))
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

    private readonly record struct WaitRoutingKey(string EventName, CorrelationId CorrelationId)
    {
        internal static WaitRoutingKey From(EventEnvelope envelope)
        {
            return new WaitRoutingKey(envelope.EventName.Value, envelope.CorrelationId);
        }
    }
}
