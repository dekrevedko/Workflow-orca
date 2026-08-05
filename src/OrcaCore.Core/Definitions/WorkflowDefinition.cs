using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// Immutable handle for one compiled workflow definition version.
/// </summary>
internal sealed record WorkflowDefinition<TState>
{
    internal WorkflowDefinition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        SequenceNode<TState> rootSequence,
        CompiledWorkflowPlan compiledPlan,
        WorkflowPolicySet? policies = null)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        ArgumentNullException.ThrowIfNull(rootSequence);
        ArgumentNullException.ThrowIfNull(compiledPlan);

        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        RootSequence = rootSequence;
        Policies = policies ?? WorkflowPolicySet.Empty;
        CompiledPlan = compiledPlan;
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
    /// Gets the immutable compiler output bound to this definition.
    /// </summary>
    internal CompiledWorkflowPlan CompiledPlan { get; }

    public SequenceNode<TState> RootSequence { get; }

    public WorkflowPolicySet Policies { get; }
}

/// <summary>
/// Cross-assembly runtime access for the implementation-only compiled definition payload.
/// </summary>
internal static class WorkflowDefinitionRuntime
{
    public static object GetPlan<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.CompiledPlan;
    }
}
