using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>Default <see cref="IInstanceRegistry"/>: an in-process, thread-safe instance table.</summary>
internal sealed class InMemoryInstanceRegistry : IInstanceRegistry
{
    private readonly ConcurrentDictionary<InstanceId, object> instances = new();

    public void Save(InstanceId instanceId, object instance) => instances[instanceId] = instance;

    public bool TryGet(InstanceId instanceId, out object? instance) => instances.TryGetValue(instanceId, out instance);
}
