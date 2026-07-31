using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using ProjectionActiveWaitSnapshot = global::OrcaCore.Abstractions.Instances.ActiveWaitSnapshot;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWaitState
{
    private readonly List<DurableActiveWait> activeWaits;
    private readonly List<DurableBufferedDelivery> bufferedDeliveries;
    private readonly List<DurablePendingResume> pendingResumes;

    private DurableWaitState(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries,
        IEnumerable<DurablePendingResume> pendingResumes)
    {
        this.activeWaits = [.. activeWaits];
        this.bufferedDeliveries = [.. bufferedDeliveries];
        this.pendingResumes = [.. pendingResumes];
    }

    internal IReadOnlyList<DurableActiveWait> ActiveWaits => [.. activeWaits];

    internal IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries => [.. bufferedDeliveries];

    internal IReadOnlyList<DurablePendingResume> PendingResumes => [.. pendingResumes];

    internal bool HasActiveWaits => activeWaits.Count > 0;

    internal static DurableWaitState FromSnapshot(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries,
        IEnumerable<DurablePendingResume>? pendingResumes = null)
    {
        ArgumentNullException.ThrowIfNull(activeWaits);
        ArgumentNullException.ThrowIfNull(bufferedDeliveries);

        return new DurableWaitState(activeWaits, bufferedDeliveries, pendingResumes ?? []);
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
        bufferedDeliveries.Clear();
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

    internal DurableBufferedDelivery? FindBufferedDelivery(
        string eventName,
        CorrelationId correlationId,
        string? branchId)
    {
        return bufferedDeliveries.FirstOrDefault(delivery =>
            Matches(eventName, correlationId, branchId, delivery));
    }

    internal DurableActiveWait? FindActiveWait(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return OrderForMatching(activeWaits.Where(wait => Matches(wait, envelope)))
            .FirstOrDefault();
    }

    internal DurableWaitReplayPlan PlanBufferedDeliveryReplay(
        DurableWaitEventContext context,
        ResumeBufferedDeliveries bufferedDeliveryHandling)
    {
        ArgumentNullException.ThrowIfNull(context);

        var events = new List<DurableWorkflowEvent>();
        var inboxWrites = new List<InboxWrite>();
        var replayWaits = activeWaits.ToList();

        foreach (var bufferedDelivery in bufferedDeliveries)
        {
            if (bufferedDeliveryHandling == ResumeBufferedDeliveries.Discard)
            {
                events.Add(new WorkflowDeliveryDiscardedEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = context.InstanceId,
                    CommandId = context.CommandId,
                    CausationId = context.CausationId,
                    OccurredAt = context.RequestedAt,
                    DiscardedEventId = bufferedDelivery.EventId
                });
                inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.DiscardedOnResume));
                continue;
            }

            var wait = OrderForMatching(
                    replayWaits.Where(candidate => Matches(candidate, bufferedDelivery)))
                .FirstOrDefault();
            if (wait is null)
            {
                inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Poisoned));
                continue;
            }

            replayWaits.RemoveAll(candidate => candidate.WaitId.Equals(wait.WaitId));
            events.Add(new WorkflowWaitMatchedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = bufferedDelivery.EventId,
                EventName = bufferedDelivery.EventName,
                CorrelationId = bufferedDelivery.CorrelationId,
                BranchId = bufferedDelivery.BranchId,
                PayloadContentType = bufferedDelivery.PayloadContentType,
                Payload = bufferedDelivery.Payload,
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            });
            inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Applied));
        }

        return new DurableWaitReplayPlan(events, inboxWrites);
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
                    waitRegistered.Mode,
                    waitRegistered.BranchId,
                    waitRegistered.TimeoutTimerId)
                {
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
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId.Equals(waitMatched.MatchedEventId));
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
            case WorkflowDeliveryBufferedEvent deliveryBuffered:
                bufferedDeliveries.Add(new DurableBufferedDelivery(
                    deliveryBuffered.BufferedEventId,
                    deliveryBuffered.EventName,
                    deliveryBuffered.CorrelationId,
                    deliveryBuffered.BranchId,
                    deliveryBuffered.PayloadContentType,
                    deliveryBuffered.Payload));
                break;
            case WorkflowDeliveryDiscardedEvent deliveryDiscarded:
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId.Equals(deliveryDiscarded.DiscardedEventId));
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
                wait.Mode,
                wait.BranchId,
                wait.TimeoutTimerId)
            {
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            })
            .ToArray();
    }

    internal IReadOnlyList<CheckpointBufferedDelivery> CreateCheckpointBufferedDeliveries()
    {
        return bufferedDeliveries
            .Select(delivery => new CheckpointBufferedDelivery(
                delivery.EventId,
                delivery.EventName,
                delivery.CorrelationId,
                delivery.BranchId,
                delivery.PayloadContentType,
                delivery.Payload))
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
                WaitSequence = pending.WaitSequence,
                FiberId = pending.FiberId,
                ScopeId = pending.ScopeId
            })
            .ToArray();
    }

    private static bool Matches(DurableActiveWait wait, EventEnvelope envelope)
    {
        return wait.EventName == envelope.EventName &&
            wait.CorrelationId.Equals(envelope.CorrelationId) &&
            BranchesMatch(wait.BranchId, envelope.BranchId);
    }

    private static bool Matches(DurableActiveWait wait, DurableBufferedDelivery delivery)
    {
        return wait.EventName == delivery.EventName &&
            wait.CorrelationId.Equals(delivery.CorrelationId) &&
            BranchesMatch(wait.BranchId, delivery.BranchId);
    }

    private static bool Matches(
        string eventName,
        CorrelationId correlationId,
        string? branchId,
        DurableBufferedDelivery delivery)
    {
        return eventName == delivery.EventName &&
            correlationId.Equals(delivery.CorrelationId) &&
            BranchesMatch(branchId, delivery.BranchId);
    }

    private static bool BranchesMatch(string? waitBranchId, string? eventBranchId)
    {
        return string.IsNullOrWhiteSpace(waitBranchId) ||
            string.IsNullOrWhiteSpace(eventBranchId) ||
            string.Equals(waitBranchId, eventBranchId, StringComparison.Ordinal);
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

internal sealed record DurableWaitReplayPlan(
    IReadOnlyList<DurableWorkflowEvent> Events,
    IReadOnlyList<InboxWrite> InboxWrites);

internal sealed record DurableWaitEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt)
{
    internal CausationId CausationId => new(CommandId.Value);
}
