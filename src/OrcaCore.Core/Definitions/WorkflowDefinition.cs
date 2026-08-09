using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Definitions;

/// <summary>
/// Immutable handle for one compiled workflow definition version.
/// </summary>
internal abstract record WorkflowDefinitionRuntimeMetadata
{
    internal abstract IReadOnlyList<ResourcePoolName> RequiredDurablePools { get; }

    internal abstract bool ContainsPublish { get; }
}

internal sealed record WorkflowDefinition<TState> : WorkflowDefinitionRuntimeMetadata
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

    internal override IReadOnlyList<ResourcePoolName> RequiredDurablePools =>
        CompiledPlan.Instructions
            .Where(instruction => instruction.StaticLeaseRequest is not null)
            .SelectMany(instruction => instruction.StaticLeaseRequest!.Requirements)
            .Select(requirement => requirement.Pool)
            .ToArray();

    internal override bool ContainsPublish =>
        CompiledPlan.Instructions.Any(instruction => instruction.Kind == CompiledInstructionKind.Publish);

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
