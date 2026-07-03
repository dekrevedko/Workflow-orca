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

    private DurableWaitState(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries)
    {
        this.activeWaits = [.. activeWaits];
        this.bufferedDeliveries = [.. bufferedDeliveries];
    }

    internal IReadOnlyList<DurableActiveWait> ActiveWaits => [.. activeWaits];

    internal IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries => [.. bufferedDeliveries];

    internal bool HasActiveWaits => activeWaits.Count > 0;

    internal static DurableWaitState FromSnapshot(
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries)
    {
        ArgumentNullException.ThrowIfNull(activeWaits);
        ArgumentNullException.ThrowIfNull(bufferedDeliveries);

        return new DurableWaitState(activeWaits, bufferedDeliveries);
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
                MatchedEventId = bufferedDelivery.EventId
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
                    waitRegistered.BranchId));
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                Remove(waitMatched.WaitId);
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId == waitMatched.MatchedEventId);
                break;
            case WorkflowDeliveryBufferedEvent deliveryBuffered:
                bufferedDeliveries.Add(new DurableBufferedDelivery(
                    deliveryBuffered.BufferedEventId,
                    deliveryBuffered.EventName,
                    deliveryBuffered.CorrelationId,
                    deliveryBuffered.BranchId));
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
                wait.BranchId))
            .ToArray();
    }

    internal IReadOnlyList<CheckpointBufferedDelivery> CreateCheckpointBufferedDeliveries()
    {
        return bufferedDeliveries
            .Select(delivery => new CheckpointBufferedDelivery(
                delivery.EventId,
                delivery.EventName,
                delivery.CorrelationId,
                delivery.BranchId))
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
