using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

public sealed class WorkflowDefinition<TState>(string definitionId, IReadOnlyList<IStep<TState>> steps)
{
    public string DefinitionId { get; } = definitionId;
    internal IReadOnlyList<IStep<TState>> Steps { get; } = steps;
}
