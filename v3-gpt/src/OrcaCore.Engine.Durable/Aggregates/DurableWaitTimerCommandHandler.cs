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

        var decision = new DurableDecision([
            new WorkflowWaitRegisteredEvent
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
                BranchId = command.BranchId
            }
        ]);

        var matched = aggregate.WaitState.FindBufferedDelivery(command.EventName, command.CorrelationId, command.BranchId);
        if (matched is null)
        {
            return new DurableDecision(
                decision.Events,
                null,
                command.Mode == WaitMode.Cold);
        }

        return new DurableDecision(
            [
                .. decision.Events,
                new WorkflowWaitMatchedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    WaitId = command.WaitId,
                    MatchedEventId = matched.EventId
                }
            ],
            null,
            false,
            [new InboxWrite(matched.EventId, InboxRecordState.Applied)]);
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

        return new DurableDecision([
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
        ]);
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

        return new DurableDecision([
            new WorkflowTimerFiredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId
            }
        ]);
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

        return new DurableDecision([
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = command.Envelope.EventId
            }
        ]);
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
            BranchId = command.Envelope.BranchId
        };
    }
}
