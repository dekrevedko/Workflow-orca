using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableResourcePoolState
{
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
        ResourcePoolAcquireResult acquireResult)
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
                        EventId = EventId.New(),
                        InstanceId = context.InstanceId,
                        CommandId = context.CommandId,
                        CausationId = context.CausationId,
                        OccurredAt = context.RequestedAt,
                        ParentInstanceId = context.ParentInstanceId,
                        RootInstanceId = context.RootInstanceId,
                        HolderKey = holderKey,
                        Tickets = acquireResult.Tickets
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
                        EventId = EventId.New(),
                        InstanceId = context.InstanceId,
                        CommandId = context.CommandId,
                        CausationId = context.CausationId,
                        OccurredAt = context.RequestedAt,
                        ParentInstanceId = context.ParentInstanceId,
                        RootInstanceId = context.RootInstanceId,
                        WaitId = WaitId.New(),
                        HolderKey = holderKey,
                        Requirements = requirements,
                        ExpiresAt = expiresAt
                    }
                ],
                true);
        }

        return DurableResourcePoolAcquirePlan.Empty;
    }

    internal IReadOnlyList<WorkflowResourcePoolReleasedEvent> CreateReleaseEvents(
        DurableResourcePoolEventContext context,
        string? holderKey = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return activeTickets
            .Where(ticket => holderKey is null || string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
            .GroupBy(ticket => ticket.HolderKey, StringComparer.Ordinal)
            .Select(group => new WorkflowResourcePoolReleasedEvent
            {
                EventId = EventId.New(),
                InstanceId = context.InstanceId,
                CommandId = context.CommandId,
                CausationId = context.CausationId,
                OccurredAt = context.RequestedAt,
                ParentInstanceId = context.ParentInstanceId,
                RootInstanceId = context.RootInstanceId,
                HolderKey = group.Key,
                Tickets = group.ToArray()
            })
            .ToArray();
    }

    internal DurableResourcePoolReplayEffects Apply(WorkflowEvent workflowEvent)
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
                            "ResourcePoolGranted",
                            new CorrelationId(queued.HolderKey),
                            queued.OccurredAt,
                            WaitMode.Cold)
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
    IReadOnlyList<WorkflowEvent> Events,
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
    InstanceId RootInstanceId)
{
    internal CausationId CausationId => new(CommandId.Value);
}
