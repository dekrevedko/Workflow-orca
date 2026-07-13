
namespace OrcaCore.Runtime.Durable.Execution;

public sealed record DurableInstanceSnapshot(
    string InstanceId,
    string DefinitionId,
    string DefinitionVersion,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastTransitionAt,
    int ActiveWaitCount,
    int ConcurrencyToken,
    WorkflowError? Error = null);
