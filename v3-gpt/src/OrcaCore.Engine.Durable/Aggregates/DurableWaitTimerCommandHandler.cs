using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

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

        var matched = aggregate.WaitState.FindBufferedDelivery(command.EventName, command.CorrelationId, command.BranchId);
        var registerTimeoutTimer = matched is null && command.TimeoutTimerId is not null;
        var events = new List<WorkflowEvent>();
        AddConsumeAndCancelEvents(events, aggregate, command);
        events.Add(new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            WaitId = command.WaitId,
            EventName = command.EventName,
            CorrelationId = command.CorrelationId,
            Mode = command.Mode,
            BranchId = command.BranchId,
            TimeoutTimerId = registerTimeoutTimer ? command.TimeoutTimerId : null
        });

        if (registerTimeoutTimer)
        {
            events.Add(new WorkflowTimerScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimeoutTimerId!.Value,
                FireAt = command.TimeoutFireAt ?? command.RequestedAt,
                WakeupName = $"wait-timeout:{command.WaitId}"
            });
        }

        IReadOnlyList<InboxWrite> inboxWrites = [];
        if (matched is not null)
        {
            events.Add(new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = command.WaitId,
                MatchedEventId = matched.EventId,
                EventName = matched.EventName,
                CorrelationId = matched.CorrelationId,
                BranchId = matched.BranchId,
                PayloadContentType = matched.PayloadContentType,
                Payload = matched.Payload
            });
            inboxWrites = [new InboxWrite(matched.EventId, InboxRecordState.Applied)];
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
            matched is null && command.Mode == WaitMode.Cold,
            inboxWrites);
    }

    internal static void AddResumeConsumedEvents(
        List<WorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        IReadOnlyList<WaitId> consumedResumeWaitIds)
    {
        foreach (var waitId in consumedResumeWaitIds)
        {
            if (aggregate.WaitState.FindPendingResume(waitId) is null)
            {
                continue;
            }

            events.Add(new WorkflowResumeConsumedEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(commandId),
                OccurredAt = requestedAt,
                WaitId = waitId
            });
        }
    }

    private static void AddConsumeAndCancelEvents(
        List<WorkflowEvent> events,
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
            if (!aggregate.WaitState.HasWait(waitId))
            {
                continue;
            }

            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = waitId
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
                EventId = EventId.New(),
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

        return new DurableDecision([
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = command.WaitId,
                MatchedEventId = command.MatchedEventId
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

        var events = new List<WorkflowEvent>
        {
            new WorkflowTimerScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId,
                FireAt = command.FireAt,
                WakeupName = command.WakeupName
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

        if (aggregate.Status == WorkflowStatus.Paused)
        {
            return new DurableDecision([
                new WorkflowTimerBufferedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    TimerId = command.TimerId,
                    WakeupName = timer.WakeupName
                }
            ]);
        }

        var events = new List<WorkflowEvent>
        {
            new WorkflowTimerFiredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId
            }
        };

        // A wait-timeout race is arbitrated atomically: the winning timer cancels its wait in
        // the same commit, so a late matching event can never double-resume (DR-AC-020).
        if (aggregate.WaitState.FindByTimeoutTimer(command.TimerId) is { } racedWait)
        {
            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = racedWait.WaitId
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

        if (aggregate.Status == WorkflowStatus.Paused)
        {
            return new DurableDecision([
                CreateBufferedEvent(command)
            ]);
        }

        var wait = aggregate.WaitState.FindActiveWait(command.Envelope);
        if (wait is null)
        {
            return new DurableDecision([
                CreateBufferedEvent(command)
            ]);
        }

        var events = new List<WorkflowEvent>
        {
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = command.Envelope.EventId,
                EventName = command.Envelope.EventName,
                CorrelationId = command.Envelope.CorrelationId,
                BranchId = command.Envelope.BranchId,
                PayloadContentType = command.Envelope.PayloadContentType,
                Payload = command.Envelope.Payload as byte[]
            }
        };

        // The matched wait's timeout timer loses the race and is cancelled in the same commit.
        if (wait.TimeoutTimerId is { } timeoutTimerId &&
            aggregate.TimerState.FindActive(timeoutTimerId) is not null)
        {
            events.Add(new WorkflowTimerCancelledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = timeoutTimerId
            });
        }

        return new DurableDecision(events);
    }

    private static WorkflowDeliveryBufferedEvent CreateBufferedEvent(DeliverEventCommand command)
    {
        return new WorkflowDeliveryBufferedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            BufferedEventId = command.Envelope.EventId,
            EventName = command.Envelope.EventName,
            CorrelationId = command.Envelope.CorrelationId,
            BranchId = command.Envelope.BranchId,
            PayloadContentType = command.Envelope.PayloadContentType,
            Payload = command.Envelope.Payload as byte[]
        };
    }
}
