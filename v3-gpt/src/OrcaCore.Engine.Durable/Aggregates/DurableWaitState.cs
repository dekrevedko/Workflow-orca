using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

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
        return activeWaits.Any(wait => wait.WaitId == waitId);
    }

    internal void Register(DurableActiveWait wait)
    {
        ArgumentNullException.ThrowIfNull(wait);
        activeWaits.Add(wait);
    }

    internal void Remove(WaitId waitId)
    {
        activeWaits.RemoveAll(wait => wait.WaitId == waitId);
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
        return pendingResumes.FirstOrDefault(pending => pending.WaitId == waitId);
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

        var matches = activeWaits.Where(wait => Matches(wait, envelope)).ToArray();
        if (string.IsNullOrWhiteSpace(envelope.BranchId) &&
            matches.Count(wait => !string.IsNullOrWhiteSpace(wait.BranchId)) > 1)
        {
            return null;
        }

        return matches.FirstOrDefault();
    }

    internal DurableWaitReplayPlan PlanBufferedDeliveryReplay(
        DurableWaitEventContext context,
        ResumeBufferedDeliveries bufferedDeliveryHandling)
    {
        ArgumentNullException.ThrowIfNull(context);

        var events = new List<WorkflowEvent>();
        var inboxWrites = new List<InboxWrite>();
        var replayWaits = activeWaits.ToList();

        foreach (var bufferedDelivery in bufferedDeliveries)
        {
            if (bufferedDeliveryHandling == ResumeBufferedDeliveries.Discard)
            {
                events.Add(new WorkflowDeliveryDiscardedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = context.InstanceId,
                    CommandId = context.CommandId,
                    CausationId = context.CausationId,
                    OccurredAt = context.RequestedAt,
                    DiscardedEventId = bufferedDelivery.EventId
                });
                inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.DiscardedOnResume));
                continue;
            }

            var wait = replayWaits.FirstOrDefault(candidate => Matches(candidate, bufferedDelivery));
            if (wait is null)
            {
                inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Poisoned));
                continue;
            }

            replayWaits.RemoveAll(candidate => candidate.WaitId == wait.WaitId);
            events.Add(new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
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
                Payload = bufferedDelivery.Payload
            });
            inboxWrites.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Applied));
        }

        return new DurableWaitReplayPlan(events, inboxWrites);
    }

    internal void Apply(WorkflowEvent workflowEvent)
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
                    waitRegistered.TimeoutTimerId));
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                // Kernel-driven matches (external-job completion, direct wait-matched commands)
                // carry no event context of their own; the resume falls back to the matched
                // wait's registered name/correlation so the resumed step observes them.
                var matchedWait = activeWaits.FirstOrDefault(wait => wait.WaitId == waitMatched.WaitId);
                Remove(waitMatched.WaitId);
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId == waitMatched.MatchedEventId);
                pendingResumes.RemoveAll(pending => pending.WaitId == waitMatched.WaitId);
                pendingResumes.Add(new DurablePendingResume(
                    waitMatched.WaitId,
                    waitMatched.MatchedEventId,
                    waitMatched.EventName ?? matchedWait?.EventName,
                    waitMatched.CorrelationId ?? matchedWait?.CorrelationId,
                    waitMatched.BranchId,
                    waitMatched.PayloadContentType,
                    waitMatched.Payload,
                    waitMatched.OccurredAt));
                break;
            case WorkflowWaitCancelledEvent waitCancelled:
                Remove(waitCancelled.WaitId);
                pendingResumes.RemoveAll(pending => pending.WaitId == waitCancelled.WaitId);
                break;
            case WorkflowResumeConsumedEvent resumeConsumed:
                pendingResumes.RemoveAll(pending => pending.WaitId == resumeConsumed.WaitId);
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
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId == deliveryDiscarded.DiscardedEventId);
                break;
        }
    }

    internal IReadOnlyList<ActiveWaitSnapshot> CreateActiveWaitSnapshots()
    {
        return activeWaits
            .Select(wait => new ActiveWaitSnapshot
            {
                WaitId = wait.WaitId,
                EventName = wait.EventName,
                CorrelationId = wait.CorrelationId,
                RegisteredAt = wait.RegisteredAt,
                BranchId = wait.BranchId,
                Status = "Active",
                Mode = wait.Mode.ToString()
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
                wait.TimeoutTimerId))
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
                pending.MatchedAt))
            .ToArray();
    }

    private static bool Matches(DurableActiveWait wait, EventEnvelope envelope)
    {
        return wait.EventName == envelope.EventName &&
            wait.CorrelationId == envelope.CorrelationId &&
            BranchesMatch(wait.BranchId, envelope.BranchId);
    }

    private static bool Matches(DurableActiveWait wait, DurableBufferedDelivery delivery)
    {
        return wait.EventName == delivery.EventName &&
            wait.CorrelationId == delivery.CorrelationId &&
            BranchesMatch(wait.BranchId, delivery.BranchId);
    }

    private static bool Matches(
        string eventName,
        CorrelationId correlationId,
        string? branchId,
        DurableBufferedDelivery delivery)
    {
        return eventName == delivery.EventName &&
            correlationId == delivery.CorrelationId &&
            BranchesMatch(branchId, delivery.BranchId);
    }

    private static bool BranchesMatch(string? waitBranchId, string? eventBranchId)
    {
        return string.IsNullOrWhiteSpace(waitBranchId) ||
            string.IsNullOrWhiteSpace(eventBranchId) ||
            string.Equals(waitBranchId, eventBranchId, StringComparison.Ordinal);
    }
}

internal sealed record DurableWaitReplayPlan(
    IReadOnlyList<WorkflowEvent> Events,
    IReadOnlyList<InboxWrite> InboxWrites);

internal sealed record DurableWaitEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt)
{
    internal CausationId CausationId => new(CommandId.Value);
}
