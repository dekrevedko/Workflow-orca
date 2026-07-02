using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Immutable, metadata-only view of an instance (CR-021). Public APIs return this instead of
/// live internal instance objects; business state is read separately via a typed accessor
/// that returns a copy.
/// </summary>
public sealed record WorkflowInstanceSnapshot(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ErrorSummary,
    string? EndOutcomeName);
