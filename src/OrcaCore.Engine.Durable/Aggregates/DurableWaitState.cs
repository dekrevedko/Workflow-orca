using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using ProjectionActiveWaitSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWaitState
{
    private readonly List<DurableActiveWait> activeWaits;
    private readonly List<DurablePendingResume> pendingResumes;

    private DurableWaitState(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurablePendingResume> pendingResumes)
    {
        this.activeWaits = [.. activeWaits];
        this.pendingResumes = [.. pendingResumes];
    }

    internal IReadOnlyList<DurableActiveWait> ActiveWaits => [.. activeWaits];

    internal IReadOnlyList<DurablePendingResume> PendingResumes => [.. pendingResumes];

    internal bool HasActiveWaits => activeWaits.Count > 0;

    internal static DurableWaitState FromSnapshot(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurablePendingResume>? pendingResumes = null)
    {
        ArgumentNullException.ThrowIfNull(activeWaits);

        return new DurableWaitState(activeWaits, pendingResumes ?? []);
    }

    internal bool HasWait(WaitId waitId)
    {
        return activeWaits.Any(wait => wait.WaitId.Equals(waitId));
    }

    internal void Register(DurableActiveWait wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        activeWaits.Add(wait);
    }

    internal void Remove(WaitId waitId)
    {
        activeWaits.RemoveAll(wait => wait.WaitId.Equals(waitId));
    }

    internal void Clear()
    {
        activeWaits.Clear();
        pendingResumes.Clear();
    }

    internal DurableActiveWait? FindByTimeoutTimer(TimerId timerId)
    {
        return activeWaits.FirstOrDefault(wait => wait.TimeoutTimerId == timerId);
    }

    internal DurablePendingResume? FindPendingResume(WaitId waitId)
    {
        return pendingResumes.FirstOrDefault(pending => pending.WaitId.Equals(waitId));
    }

    internal DurableActiveWait? FindActiveWait(DurableEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return OrderForMatching(activeWaits.Where(wait => Matches(wait, envelope)))
            .FirstOrDefault();
    }

    internal void Apply(DurableWorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        switch (workflowEvent)
        {
            case WorkflowWaitRegisteredEvent waitRegistered:
                Register(new DurableActiveWait(
                    waitRegistered.WaitId,
                    waitRegistered.EventName,
                    waitRegistered.CorrelationId,
                    waitRegistered.OccurredAt,
                    DurableWaitResidency.FromProtocol(waitRegistered.Mode),
                    waitRegistered.BranchId,
                    waitRegistered.TimeoutTimerId)
                {
                    EventContractVersion = waitRegistered.EventContractVersion,
                    WaitSequence = waitRegistered.WaitSequence,
                    FiberId = waitRegistered.FiberId,
                    ScopeId = waitRegistered.ScopeId
                });
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                // Kernel-driven matches (external-job completion, direct wait-matched commands)
                // carry no event context of their own; the resume falls back to the matched
                // wait's registered name/correlation so the resumed step observes them.
                var matchedWait = activeWaits.FirstOrDefault(wait => wait.WaitId.Equals(waitMatched.WaitId));
                Remove(waitMatched.WaitId);
                pendingResumes.RemoveAll(pending => pending.WaitId.Equals(waitMatched.WaitId));
                pendingResumes.Add(new DurablePendingResume(
                    waitMatched.WaitId,
                    waitMatched.MatchedEventId,
                    waitMatched.EventName ?? matchedWait?.EventName,
                    waitMatched.CorrelationId ?? matchedWait?.CorrelationId,
                    waitMatched.BranchId,
                    waitMatched.PayloadContentType,
                    waitMatched.Payload,
                    waitMatched.OccurredAt)
                {
                    EventContractVersion = waitMatched.EventContractVersion ?? matchedWait?.EventContractVersion,
                    WaitSequence = waitMatched.WaitSequence != 0
                        ? waitMatched.WaitSequence
                        : matchedWait?.WaitSequence ?? 0,
                    FiberId = waitMatched.FiberId ?? matchedWait?.FiberId,
                    ScopeId = waitMatched.ScopeId ?? matchedWait?.ScopeId
                });
                break;
            case WorkflowWaitCancelledEvent waitCancelled:
                Remove(waitCancelled.WaitId);
                pendingResumes.RemoveAll(pending => pending.WaitId.Equals(waitCancelled.WaitId));
                break;
            case WorkflowResumeConsumedEvent resumeConsumed:
                pendingResumes.RemoveAll(pending => pending.WaitId.Equals(resumeConsumed.WaitId));
                break;
        }
    }

    internal IReadOnlyList<ProjectionActiveWaitSnapshot> CreateActiveWaitSnapshots()
    {
        return activeWaits
            .Select(wait => new ProjectionActiveWaitSnapshot
            {
                WaitId = wait.WaitId,
                EventName = wait.EventName,
                CorrelationId = wait.CorrelationId,
                RegisteredAt = wait.RegisteredAt,
                BranchId = wait.BranchId,
                Status = "Active",
                Mode = wait.Mode.ToString(),
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            })
            .ToArray();
    }

    internal IReadOnlyList<CheckpointActiveWait> CreateCheckpointActiveWaits()
    {
        return activeWaits
            .Select(wait => new CheckpointActiveWait(
                wait.WaitId,
                wait.EventName,
                wait.CorrelationId,
                wait.RegisteredAt,
                DurableWaitResidency.ToProtocol(wait.Mode),
                wait.BranchId,
                wait.TimeoutTimerId)
            {
                EventContractVersion = wait.EventContractVersion,
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            })
            .ToArray();
    }

    internal IReadOnlyList<CheckpointPendingResume> CreateCheckpointPendingResumes()
    {
        return pendingResumes
            .Select(pending => new CheckpointPendingResume(
                pending.WaitId,
                pending.MatchedEventId,
                pending.EventName,
                pending.CorrelationId,
                pending.BranchId,
                pending.PayloadContentType,
                pending.Payload,
                pending.MatchedAt)
            {
                EventContractVersion = pending.EventContractVersion,
                WaitSequence = pending.WaitSequence,
                FiberId = pending.FiberId,
                ScopeId = pending.ScopeId
            })
            .ToArray();
    }

    private static bool Matches(DurableActiveWait wait, DurableEventEnvelope envelope)
    {
        return wait.EventName == envelope.EventName &&
            wait.EventContractVersion == envelope.EventContractVersion &&
            wait.CorrelationId.Equals(envelope.CorrelationId);
    }

    private static IOrderedEnumerable<DurableActiveWait> OrderForMatching(
        IEnumerable<DurableActiveWait> waits)
    {
        return waits
            .OrderBy(wait => wait.WaitSequence)
            .ThenBy(wait => wait.FiberId?.Value ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(wait => wait.WaitId.Value);
    }
}
