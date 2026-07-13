using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Durable.Management;

/// <summary>
/// Metadata-only model accepted by durable management query predicates.
/// </summary>
public sealed record WorkflowInstanceQueryModel
{
    /// <summary>
    /// Gets the logical workflow instance identity.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the parent workflow instance when this instance is child work.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets the root workflow instance for this workflow tree.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }

    /// <summary>
    /// Gets the workflow definition identity.
    /// </summary>
    public required DefinitionId DefinitionId { get; init; }

    /// <summary>
    /// Gets the workflow definition version.
    /// </summary>
    public required DefinitionVersion DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the lifecycle status.
    /// </summary>
    public required WorkflowStatus Status { get; init; }

    /// <summary>
    /// Gets when the instance was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets when the instance metadata last changed.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets whether the instance has any current stuck signal.
    /// </summary>
    public bool IsStuck { get; init; }

    /// <summary>
    /// Gets whether any step on the instance has exceeded its stuck threshold.
    /// </summary>
    public bool HasStuckStep { get; init; }
}
