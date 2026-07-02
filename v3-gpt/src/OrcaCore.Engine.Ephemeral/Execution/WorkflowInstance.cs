using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowInstance<TState>
{
    private readonly List<RuntimeWaitRecord> activeWaits = [];
    private readonly HashSet<EventId> consumedEventIds = [];
    private readonly HashSet<WaitSignature> consumedWaits = [];
    private readonly List<EventEnvelope> pendingEvents = [];

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

    internal bool HasUnresolvedRuntimeWork => activeWaits.Count > 0 || pendingEvents.Count > 0;

    internal bool HasActiveWait(string eventName, CorrelationId correlationId)
    {
        return activeWaits.Any(wait =>
            string.Equals(wait.EventName, eventName, StringComparison.Ordinal) &&
            wait.CorrelationId == correlationId);
    }

    internal RuntimeWaitRecord EnterWait(
        string eventName,
        CorrelationId correlationId,
        BranchId? branchId,
        DateTimeOffset registeredAt,
        Func<EventEnvelope, CancellationToken, Task> resumeAsync)
    {
        Status = WorkflowStatus.Waiting;
        UpdatedAt = registeredAt;
        var wait = new RuntimeWaitRecord(eventName, correlationId, branchId, registeredAt, resumeAsync);
        activeWaits.Add(wait);
        return wait;
    }

    internal async Task<WorkflowInstanceSnapshot> RaiseEventAsync(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (HasSeen(envelope.EventId) || LifecycleMachine.TerminalStatuses.Contains(Status))
        {
            return ToSnapshot();
        }

        var wait = activeWaits.FirstOrDefault(candidate => candidate.Matches(envelope));
        if (wait is null)
        {
            if (HasConsumedWait(envelope.EventName, envelope.CorrelationId))
            {
                return ToSnapshot();
            }

            pendingEvents.Add(envelope);
            return ToSnapshot();
        }

        return await ResumeWaitAsync(wait, envelope, removePendingAfterCommit: false, cancellationToken)
            .ConfigureAwait(false);
    }

    internal async Task<WorkflowInstanceSnapshot> MatchPendingEventAsync(
        RuntimeWaitRecord wait,
        CancellationToken cancellationToken)
    {
        var envelope = pendingEvents.FirstOrDefault(wait.Matches);
        if (envelope is null)
        {
            return ToSnapshot();
        }

        return await ResumeWaitAsync(wait, envelope, removePendingAfterCommit: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<WorkflowInstanceSnapshot> ResumeWaitAsync(
        RuntimeWaitRecord wait,
        EventEnvelope envelope,
        bool removePendingAfterCommit,
        CancellationToken cancellationToken)
    {
        wait.MarkMatched();
        activeWaits.Remove(wait);
        FireOrThrow(LifecycleTrigger.MatchWait);
        Status = WorkflowStatus.Running;
        UpdatedAt = envelope.OccurredAt;

        var removedPendingEvent = false;
        if (removePendingAfterCommit)
        {
            removedPendingEvent = pendingEvents.RemoveAll(candidate => candidate.EventId == envelope.EventId) > 0;
        }

        try
        {
            await wait.ResumeAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            wait.MarkActive();
            if (!activeWaits.Contains(wait))
            {
                activeWaits.Add(wait);
            }

            if (!HasSeen(envelope.EventId))
            {
                pendingEvents.Add(envelope);
            }
            else if (removedPendingEvent && pendingEvents.All(candidate => candidate.EventId != envelope.EventId))
            {
                pendingEvents.Add(envelope);
            }

            Status = WorkflowStatus.Waiting;
            UpdatedAt = wait.RegisteredAt;
            throw;
        }

        consumedEventIds.Add(envelope.EventId);
        consumedWaits.Add(new WaitSignature(wait.EventName, wait.CorrelationId, wait.BranchId));
        if (Status == WorkflowStatus.Running && activeWaits.Count > 0)
        {
            Status = WorkflowStatus.Waiting;
        }

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

    private bool HasSeen(EventId eventId)
    {
        return consumedEventIds.Contains(eventId) || pendingEvents.Any(envelope => envelope.EventId == eventId);
    }

    private bool HasConsumedWait(string eventName, CorrelationId correlationId)
    {
        return consumedWaits.Any(wait =>
            string.Equals(wait.EventName, eventName, StringComparison.Ordinal) &&
            wait.CorrelationId == correlationId);
    }

    private readonly record struct WaitSignature(string EventName, CorrelationId CorrelationId, BranchId? BranchId);
}
