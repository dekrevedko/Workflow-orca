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
            aggregate.CreateResourcePoolEventContext(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt,
                command.FiberId,
                command.ScopeId,
                command.WaitSequence),
            command.HolderKey,
            command.Requirements,
            command.ExpiresAt,
            acquireResult,
            command.WaitId);
        if (plan.Events.Count == 0)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        DurableWaitTimerCommandHandler.AddResumeConsumedEvents(
            events, aggregate, command.CommandId, command.InstanceId, command.RequestedAt, command.ConsumedResumeWaitIds);
        events.AddRange(plan.Events);
        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint, plan.EvictAfterCommit);
    }
}
