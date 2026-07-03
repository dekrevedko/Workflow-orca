using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Metadata-only immutable view of a workflow instance returned by public APIs (CR-021).
/// Business state is never exposed through this type.
/// </summary>
/// <param name="InstanceId">Globally unique instance identity.</param>
/// <param name="DefinitionId">Bound definition identity.</param>
/// <param name="DefinitionVersion">Bound definition version.</param>
/// <param name="Status">Current lifecycle status.</param>
/// <param name="CreatedAt">UTC timestamp when the instance was created.</param>
/// <param name="UpdatedAt">UTC timestamp of the last metadata change.</param>
/// <param name="ErrorSummary">Optional human-readable failure summary when status is failed.</param>
/// <param name="EndOutcomeName">Optional terminal outcome name when the workflow completed.</param>
public sealed record WorkflowInstanceSnapshot(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ErrorSummary,
    string? EndOutcomeName);
