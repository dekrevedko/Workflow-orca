using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides one grouped workflow instance count.
/// </summary>
public sealed record WorkflowStatisticsGroup
{
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
    /// Gets the number of matching instances in the group.
    /// </summary>
    public required int Count { get; init; }
}
