using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface IInstanceRegistry
{
    void Save<TState>(WorkflowInstance<TState> instance);

    IReadOnlyCollection<object> GetMany(IReadOnlyCollection<InstanceId> instanceIds);

    bool TryGet(InstanceId instanceId, out object? instance);

    IReadOnlyCollection<object> List();

    bool Remove(InstanceId instanceId);
}
