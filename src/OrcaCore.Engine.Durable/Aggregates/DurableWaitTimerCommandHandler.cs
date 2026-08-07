using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableWaitTimerCommandHandler
{
    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableWaitRegisteredCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var cancelledWaitIds = command.CancelWaitIds.ToHashSet();
        if (aggregate.WaitState.ActiveWaits.Any(wait =>
                !cancelledWaitIds.Contains(wait.WaitId) &&
                string.Equals(wait.EventName, command.EventName, StringComparison.Ordinal) &&
                wait.EventContractVersion == command.EventContractVersion &&
                wait.CorrelationId.Equals(command.CorrelationId)))
        {
            var definitionId = aggregate.DefinitionId ??
                throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    "A durable wait cannot be registered before its definition identity is available.");
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.AmbiguousWait(
                definitionId,
                WorkflowEventContract.Create(
                    EventName.Create(command.EventName),
                    new EventContractVersion(command.EventContractVersion)),
                command.CorrelationId);
        }

        var registerTimeoutTimer = command.TimeoutTimerId is not null;
        var events = new List<DurableWorkflowEvent>();
        AddConsumeAndCancelEvents(events, aggregate, command);
        events.Add(new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            WaitId = command.WaitId,
            EventName = command.EventName,
            EventContractVersion = command.EventContractVersion,
            CorrelationId = command.CorrelationId,
            Mode = DurableWaitResidency.ToProtocol(command.Mode),
            BranchId = command.BranchId,
            TimeoutTimerId = registerTimeoutTimer ? command.TimeoutTimerId : null,
            WaitSequence = command.WaitSequence,
            FiberId = command.FiberId,
            ScopeId = command.ScopeId
        });

        if (registerTimeoutTimer)
        {
            events.Add(new WorkflowTimerScheduledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimeoutTimerId!.Value,
                FireAt = command.TimeoutFireAt ?? command.RequestedAt,
                WakeupName = $"wait-timeout:{command.WaitId}",
                FiberId = command.FiberId,
                ScopeId = command.ScopeId
            });
        }

        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(
            events,
            checkpoint,
            command.Mode == WaitMode.Cold);
    }

    internal static void AddResumeConsumedEvents(
        List<DurableWorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        IReadOnlyList<WaitId> consumedResumeWaitIds)
    {
        foreach (var waitId in consumedResumeWaitIds)
        {
            if (aggregate.WaitState.FindPendingResume(waitId) is not { } pending)
            {
                continue;
            }

            events.Add(new WorkflowResumeConsumedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(commandId),
                OccurredAt = requestedAt,
                WaitId = waitId,
                FiberId = pending.FiberId,
                ScopeId = pending.ScopeId
            });
        }
    }

    private static void AddConsumeAndCancelEvents(
        List<DurableWorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        DurableWaitRegisteredCommand command)
    {
        AddResumeConsumedEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds);

        foreach (var waitId in command.CancelWaitIds)
        {
            if (aggregate.WaitState.ActiveWaits.FirstOrDefault(wait => wait.WaitId.Equals(waitId)) is not { } wait)
            {
                continue;
            }

            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = waitId,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            });
        }

        foreach (var timerId in command.CancelTimerIds)
        {
            if (aggregate.TimerState.FindActive(timerId) is null)
            {
                continue;
            }

            events.Add(new WorkflowTimerCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = timerId
            });
        }
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableWaitMatchedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || !aggregate.WaitState.HasWait(command.WaitId))
        {
            return DurableDecision.Empty;
        }

        var wait = aggregate.WaitState.ActiveWaits
            .First(candidate => candidate.WaitId.Equals(command.WaitId));
        return new DurableDecision([
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = command.WaitId,
                MatchedEventId = command.MatchedEventId,
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            }
        ]);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, ScheduleTimerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>
        {
            new WorkflowTimerScheduledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId,
                FireAt = command.FireAt,
                WakeupName = command.WakeupName,
                FiberId = command.FiberId,
                ScopeId = command.ScopeId
            }
        };

        var checkpoint = command.Envelope is { } envelope
            ? DurableLifecycleCommandHandler.CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, FireTimerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var timer = aggregate.TimerState.FindActive(command.TimerId);
        if (aggregate.IsTerminal || timer is null)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>
        {
            new WorkflowTimerFiredEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId,
                FiberId = timer.FiberId,
                ScopeId = timer.ScopeId
            }
        };

        // A wait-timeout race is arbitrated atomically: the winning timer cancels its wait in
        // the same commit, so a late matching event can never double-resume (DR-AC-020).
        if (aggregate.WaitState.FindByTimeoutTimer(command.TimerId) is { } racedWait)
        {
            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = racedWait.WaitId,
                FiberId = racedWait.FiberId,
                ScopeId = racedWait.ScopeId
            });
        }

        return new DurableDecision(events);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DeliverEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var wait = aggregate.WaitState.FindActiveWait(command.Envelope);
        if (wait is null)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>
        {
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = command.Envelope.EventId,
                EventName = command.Envelope.EventName,
                EventContractVersion = command.Envelope.EventContractVersion,
                CorrelationId = command.Envelope.CorrelationId,
                BranchId = wait.BranchId,
                PayloadContentType = command.Envelope.PayloadContentType,
                Payload = command.Envelope.Payload as byte[],
                WaitSequence = wait.WaitSequence,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            }
        };

        // The matched wait's timeout timer loses the race and is cancelled in the same commit.
        if (wait.TimeoutTimerId is { } timeoutTimerId &&
            aggregate.TimerState.FindActive(timeoutTimerId) is not null)
        {
            events.Add(new WorkflowTimerCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = timeoutTimerId,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            });
        }

        return new DurableDecision(events);
    }
}
