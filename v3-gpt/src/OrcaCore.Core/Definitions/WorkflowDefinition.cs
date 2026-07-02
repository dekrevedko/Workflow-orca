using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// Immutable handle for one compiled workflow definition version.
/// </summary>
public sealed record WorkflowDefinition<TState>
{
    internal WorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        SequenceNode<TState> rootSequence)
    {
        ArgumentNullException.ThrowIfNull(rootSequence);

        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        RootSequence = rootSequence;
    }

    /// <summary>
    /// Gets the stable workflow definition identity.
    /// </summary>
    public DefinitionId DefinitionId { get; }

    /// <summary>
    /// Gets the immutable version represented by this definition.
    /// </summary>
    public DefinitionVersion DefinitionVersion { get; }

    internal SequenceNode<TState> RootSequence { get; }
}
