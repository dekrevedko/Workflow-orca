namespace OrcaCore.Abstractions;

public sealed record WorkflowInstanceSnapshot(
    string InstanceId,
    string DefinitionId,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastTransitionAt,
    WorkflowError? Error = null);
