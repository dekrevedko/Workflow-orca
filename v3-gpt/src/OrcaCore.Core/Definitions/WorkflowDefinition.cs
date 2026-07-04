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
        SequenceNode<TState> rootSequence,
        WorkflowPolicySet? policies = null,
        bool requiresDurableEngine = false)
    {
        ArgumentNullException.ThrowIfNull(rootSequence);

        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        RootSequence = rootSequence;
        Policies = policies ?? WorkflowPolicySet.Empty;
        RequiresDurableEngine = requiresDurableEngine;
    }

    /// <summary>
    /// Gets the stable workflow definition identity.
    /// </summary>
    public DefinitionId DefinitionId { get; }

    /// <summary>
    /// Gets the immutable version represented by this definition.
    /// </summary>
    public DefinitionVersion DefinitionVersion { get; }

    /// <summary>
    /// Gets whether the definition contains durable-only primitives (RunChild/RunChildren)
    /// and therefore cannot execute on the ephemeral engine.
    /// </summary>
    public bool RequiresDurableEngine { get; }

    internal SequenceNode<TState> RootSequence { get; }

    internal WorkflowPolicySet Policies { get; }
}
