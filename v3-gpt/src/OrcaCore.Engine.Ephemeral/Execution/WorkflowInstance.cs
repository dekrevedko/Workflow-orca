using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowInstance<TState>
{
    internal WorkflowInstance(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TState state,
        DateTimeOffset createdAt)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = WorkflowStatus.Running;
    }

    internal InstanceId InstanceId { get; }

    internal DefinitionId DefinitionId { get; }

    internal DefinitionVersion DefinitionVersion { get; }

    internal TState State { get; }

    internal WorkflowStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal WorkflowErrorDetails? ErrorDetails { get; private set; }

    internal string? EndOutcomeName { get; private set; }

    internal void Complete(string? outcomeName, DateTimeOffset updatedAt)
    {
        Status = WorkflowStatus.Completed;
        EndOutcomeName = outcomeName;
        UpdatedAt = updatedAt;
    }

    internal void Fail(WorkflowErrorDetails errorDetails)
    {
        Status = WorkflowStatus.Failed;
        ErrorDetails = errorDetails;
        UpdatedAt = errorDetails.OccurredAt;
    }

    internal WorkflowInstanceSnapshot ToSnapshot()
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = InstanceId,
            DefinitionId = DefinitionId,
            DefinitionVersion = DefinitionVersion,
            Status = Status,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            ErrorSummary = ErrorDetails?.Summary,
            EndOutcomeName = EndOutcomeName
        };
    }
}
