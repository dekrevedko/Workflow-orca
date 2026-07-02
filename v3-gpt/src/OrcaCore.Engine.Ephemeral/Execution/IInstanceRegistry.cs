using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface IInstanceRegistry
{
    void Save<TState>(WorkflowInstance<TState> instance);

    bool TryGet(InstanceId instanceId, out object? instance);
}
