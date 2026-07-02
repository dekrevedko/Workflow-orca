using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides an immutable metadata-only view of a workflow instance.
/// </summary>
public sealed record WorkflowInstanceSnapshot
{
    /// <summary>
    /// Gets the logical workflow instance identity.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the workflow definition identity.
    /// </summary>
    public required DefinitionId DefinitionId { get; init; }

    /// <summary>
    /// Gets the workflow definition version bound at start.
    /// </summary>
    public required DefinitionVersion DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the current lifecycle status.
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
    /// Gets the failure summary when status is failed.
    /// </summary>
    public string? ErrorSummary { get; init; }

    /// <summary>
    /// Gets the named end outcome when the definition ended with one.
    /// </summary>
    public string? EndOutcomeName { get; init; }

    /// <summary>
    /// Gets immutable snapshots of currently active waits.
    /// </summary>
    public IReadOnlyList<ActiveWaitSnapshot> ActiveWaits { get; init; } = [];
}
