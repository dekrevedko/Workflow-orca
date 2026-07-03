using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// In-memory <see cref="IInstanceRegistry"/> backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Instances are stored boxed as <see cref="object"/> since the registry is not generic over
/// <c>TState</c>; the engine facade always knows the concrete <c>TState</c> at the read site.
/// </summary>
internal sealed class InstanceRegistry : IInstanceRegistry
{
    private readonly ConcurrentDictionary<InstanceId, object> instances = new();

    public void Add<TState>(WorkflowInstance<TState> instance) =>
        instances[instance.InstanceId] = instance;

    public WorkflowInstance<TState>? TryGet<TState>(InstanceId instanceId) =>
        instances.TryGetValue(instanceId, out var untyped) && untyped is WorkflowInstance<TState> typed
            ? typed
            : null;

    public IWorkflowInstance? TryGetUntyped(InstanceId instanceId) =>
        instances.TryGetValue(instanceId, out var untyped) ? (IWorkflowInstance)untyped : null;

    public IReadOnlyList<IWorkflowInstance> GetAll() =>
        [.. instances.Values.Cast<IWorkflowInstance>()];
}
