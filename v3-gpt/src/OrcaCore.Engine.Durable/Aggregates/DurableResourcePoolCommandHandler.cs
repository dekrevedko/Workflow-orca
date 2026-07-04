using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableResourcePoolCommandHandler
{
    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        AcquireResourcePoolCommand command,
        ResourcePoolAcquireResult acquireResult)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acquireResult);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var plan = aggregate.ResourcePoolState.PlanAcquire(
            aggregate.CreateResourcePoolEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.HolderKey,
            command.Requirements,
            command.ExpiresAt,
            acquireResult);
        return plan.Events.Count == 0
            ? DurableDecision.Empty
            : new DurableDecision(plan.Events, null, plan.EvictAfterCommit);
    }
}
