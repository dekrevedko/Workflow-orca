using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Seam over instance storage so the public engine facade never depends on a concrete
/// in-memory collection. Instances are stored as <see cref="object"/> because the registry is
/// not generic over <c>TState</c>; callers cast back to <c>WorkflowInstance&lt;TState&gt;</c>
/// using the type they registered the definition with.
/// </summary>
internal interface IInstanceRegistry
{
    void Save(InstanceId instanceId, object instance);

    bool TryGet(InstanceId instanceId, out object? instance);
}
