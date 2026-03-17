using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Definitions;

internal sealed class DurableRegisteredDefinition(
    string definitionId,
    string definitionVersion,
    Type stateType,
    Func<PersistedInstance, IWorkflowInstance> load)
{
    public string DefinitionId { get; } = definitionId;

    public string DefinitionVersion { get; } = definitionVersion;

    public Type StateType { get; } = stateType;

    public Func<PersistedInstance, IWorkflowInstance> Load { get; } = load;
}
