using OrcaCore.Abstractions.Enums;

namespace OrcaCore.EventDrivenPrototype.Projections;

public sealed record InstanceSummaryProjection(
    string InstanceId,
    string DefinitionId,
    string DefinitionVersion,
    WorkflowStatus Status,
    int ActiveWaitCount,
    int StreamVersion,
    DateTimeOffset UpdatedAt);
