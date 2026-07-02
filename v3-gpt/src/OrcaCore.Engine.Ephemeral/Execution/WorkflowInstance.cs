using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowInstance<TState>
{
    private readonly List<RuntimeWaitRecord> activeWaits = [];

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

    internal void EnterWait(
        string eventName,
        CorrelationId correlationId,
        DateTimeOffset registeredAt,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync)
    {
        Status = WorkflowStatus.Waiting;
        UpdatedAt = registeredAt;
        activeWaits.Add(new RuntimeWaitRecord(eventName, correlationId, registeredAt, resumeAsync));
    }

    internal async Task<WorkflowInstanceSnapshot> RaiseEventAsync(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var wait = activeWaits.FirstOrDefault(candidate => candidate.Matches(envelope));
        if (wait is null)
        {
            return ToSnapshot();
        }

        wait.MarkMatched();
        activeWaits.Remove(wait);
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = WorkflowStatus.Running;
        UpdatedAt = envelope.OccurredAt;

        await wait.ResumeAsync(envelope, cancellationToken).ConfigureAwait(false);
        return ToSnapshot();
    }

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
            EndOutcomeName = EndOutcomeName,
            ActiveWaits = activeWaits.Select(wait => wait.ToSnapshot()).ToArray()
        };
    }

    private void FireOrThrow(LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }
    }
}
