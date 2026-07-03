using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// An immutable, built workflow definition graph (CR-002, CR-004). Runtime execution never
/// depends on mutable definition state.
/// </summary>
internal sealed record WorkflowDefinition<TState>(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    SequenceNode Root);
