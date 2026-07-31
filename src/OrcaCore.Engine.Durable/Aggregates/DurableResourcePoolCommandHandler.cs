using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableResourcePoolCommandHandler
{
    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        DurableLeaseStopConfirmedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.HolderKey);
        ArgumentNullException.ThrowIfNull(command.Envelope);

        var events = new List<DurableWorkflowEvent>();
        events.AddRange(aggregate.ResourcePoolState.CreateReleaseEvents(
            aggregate.CreateResourcePoolEventContext(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt),
            command.HolderKey));
        events.Add(new WorkflowStepCompletedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            StepPath = $"resource-lease-confirmed:{command.HolderKey}"
        });
        return new DurableDecision(
            events,
            DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                command.Envelope,
                aggregate.LastStepPath));
    }

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

        var events = new List<DurableWorkflowEvent>();
        DurableWaitTimerCommandHandler.AddResumeConsumedEvents(
            events, aggregate, command.CommandId, command.InstanceId, command.RequestedAt, command.ConsumedResumeWaitIds);
        if (acquireResult.Status == ResourcePoolAcquireStatus.Granted &&
            command.WaitId is { } waitId &&
            aggregate.WaitState.ActiveWaits.FirstOrDefault(wait => wait.WaitId.Equals(waitId)) is { } activeWait)
        {
            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = waitId,
                FiberId = activeWait.FiberId,
                ScopeId = activeWait.ScopeId
            });
        }

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
