using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InMemoryInstanceRegistry : IInstanceRegistry
{
    private readonly ConcurrentDictionary<InstanceId, object> instances = [];

    public void Save<TState>(WorkflowInstance<TState> instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        instances[instance.InstanceId] = instance;
    }

    public bool TryGet(InstanceId instanceId, out object? instance)
    {
        return instances.TryGetValue(instanceId, out instance);
    }

    public IReadOnlyCollection<object> GetMany(IReadOnlyCollection<InstanceId> instanceIds)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        return instanceIds
            .Where(instances.ContainsKey)
            .Select(instanceId => instances[instanceId])
            .ToArray();
    }

    public IReadOnlyCollection<object> List()
    {
        return instances.Values.ToArray();
    }

    public bool Remove(InstanceId instanceId)
    {
        return instances.TryRemove(instanceId, out _);
    }
}
