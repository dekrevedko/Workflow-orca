using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableResourcePoolState
{
    /// <summary>
    /// The reserved event name that signals a queued holder to re-attempt its acquisition.
    /// Delivered like any correlated event (correlation = holder key); the driver re-runs the
    /// guarded node instead of advancing past it.
    /// </summary>
    internal const string GrantedEventName = "ResourcePoolGranted";

    private readonly List<ResourcePoolTicket> activeTickets;

    private DurableResourcePoolState(IEnumerable<ResourcePoolTicket> activeTickets)
    {
        this.activeTickets = [.. activeTickets];
    }

    internal IReadOnlyList<ResourcePoolTicket> ActiveTickets => [.. activeTickets];

    internal static DurableResourcePoolState FromSnapshot(IEnumerable<ResourcePoolTicket> activeTickets)
    {
        ArgumentNullException.ThrowIfNull(activeTickets);

        return new DurableResourcePoolState(activeTickets);
    }

    internal void Clear()
    {
        activeTickets.Clear();
    }

    internal DurableResourcePoolAcquirePlan PlanAcquire(
        DurableResourcePoolEventContext context,
        string holderKey,
        IReadOnlyList<ResourcePoolRequirement> requirements,
        DateTimeOffset? expiresAt,
        ResourcePoolAcquireResult acquireResult,
        WaitId? queuedWaitId = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(holderKey);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(acquireResult);

        if (acquireResult.Status == ResourcePoolAcquireStatus.Granted)
        {
            return new DurableResourcePoolAcquirePlan(
                [
                    new WorkflowResourcePoolAcquiredEvent
                    {
                        EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                        InstanceId = context.InstanceId,
                        CommandId = context.CommandId,
                        CausationId = context.CausationId,
                        OccurredAt = context.RequestedAt,
                        ParentInstanceId = context.ParentInstanceId,
                        RootInstanceId = context.RootInstanceId,
                        HolderKey = holderKey,
                        Tickets = acquireResult.Tickets,
                        FiberId = context.FiberId,
                        ScopeId = context.ScopeId
                    }
                ],
                false);
        }

        if (acquireResult.Status == ResourcePoolAcquireStatus.Queued)
        {
            return new DurableResourcePoolAcquirePlan(
                [
                    new WorkflowResourcePoolQueuedEvent
                    {
                        EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                        InstanceId = context.InstanceId,
                        CommandId = context.CommandId,
                        CausationId = context.CausationId,
                        OccurredAt = context.RequestedAt,
                        ParentInstanceId = context.ParentInstanceId,
                        RootInstanceId = context.RootInstanceId,
                        WaitId = queuedWaitId ?? WaitId.Parse(Guid.CreateVersion7().ToString()),
                        HolderKey = holderKey,
                        Requirements = requirements,
                        ExpiresAt = expiresAt,
                        WaitSequence = context.WaitSequence,
                        FiberId = context.FiberId,
                        ScopeId = context.ScopeId
                    }
                ],
                true);
        }

        return DurableResourcePoolAcquirePlan.Empty;
    }

    internal IReadOnlyList<WorkflowResourcePoolReleasedEvent> CreateReleaseEvents(
        DurableResourcePoolEventContext context,
        string? holderKey = null,
        IReadOnlySet<FiberId>? ownerFiberIds = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return activeTickets
            .Where(ticket => holderKey is null || string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
            .Where(ticket => ownerFiberIds is null ||
                ticket.FiberId is { } fiberId && ownerFiberIds.Contains(fiberId))
            .GroupBy(ticket => ticket.HolderKey, StringComparer.Ordinal)
            .Select(group => new WorkflowResourcePoolReleasedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                ParentInstanceId = context.ParentInstanceId,
                RootInstanceId = context.RootInstanceId,
                HolderKey = group.Key,
                Tickets = group.ToArray(),
                FiberId = group.First().FiberId,
                ScopeId = group.First().ScopeId
            })
            .ToArray();
    }

    internal DurableResourcePoolReplayEffects Apply(DurableWorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        switch (workflowEvent)
        {
            case WorkflowResourcePoolAcquiredEvent acquired:
                RemoveTickets(acquired.HolderKey);
                activeTickets.AddRange(acquired.Tickets);
                return DurableResourcePoolReplayEffects.Empty;
            case WorkflowResourcePoolQueuedEvent queued:
                return new DurableResourcePoolReplayEffects(
                    [
                        new DurableActiveWait(
                            queued.WaitId,
                            GrantedEventName,
                            CorrelationId.Create(queued.HolderKey),
                            queued.OccurredAt,
                            WaitMode.Cold)
                        {
                            WaitSequence = queued.WaitSequence,
                            FiberId = queued.FiberId,
                            ScopeId = queued.ScopeId
                        }
                    ]);
            case WorkflowResourcePoolReleasedEvent released:
                RemoveTickets(released.HolderKey);
                return DurableResourcePoolReplayEffects.Empty;
            default:
                return DurableResourcePoolReplayEffects.Empty;
        }
    }

    internal IReadOnlyList<ResourcePoolTicket> CreateCheckpointActiveResourceTickets()
    {
        return [.. activeTickets];
    }

    private void RemoveTickets(string holderKey)
    {
        activeTickets.RemoveAll(ticket =>
            string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal));
    }
}

internal sealed record DurableResourcePoolAcquirePlan(
    IReadOnlyList<DurableWorkflowEvent> Events,
    bool EvictAfterCommit)
{
    internal static DurableResourcePoolAcquirePlan Empty { get; } = new([], false);
}

internal sealed record DurableResourcePoolReplayEffects(IReadOnlyList<DurableActiveWait> WaitsToRegister)
{
    internal static DurableResourcePoolReplayEffects Empty { get; } = new([]);
}

internal sealed record DurableResourcePoolEventContext(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId? ParentInstanceId,
    InstanceId RootInstanceId,
    FiberId? FiberId = null,
    ScopeId? ScopeId = null,
    long WaitSequence = 0)
{
    internal CausationId CausationId => new(CommandId.Value);
}
