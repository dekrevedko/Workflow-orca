using OrcaCore.Abstractions.Contracts;

namespace OrcaCore.EventDrivenPrototype.Definitions;

public sealed class EventDrivenWorkflowDefinition<TState>(
    string definitionId,
    string definitionVersion,
    IReadOnlyList<IStep<TState>> steps)
{
    public string DefinitionId { get; } = definitionId;
    public string DefinitionVersion { get; } = definitionVersion;
    public IReadOnlyList<IStep<TState>> Steps { get; } = steps;
}
