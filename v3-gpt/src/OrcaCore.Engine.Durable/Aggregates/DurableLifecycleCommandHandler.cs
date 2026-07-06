using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableLifecycleCommandHandler
{
    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, StartWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.Status is not null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowStartedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = command.ParentInstanceId,
                RootInstanceId = command.RootInstanceId ?? command.InstanceId,
                DefinitionId = command.DefinitionId,
                DefinitionVersion = command.DefinitionVersion,
                IdempotencyKey = command.IdempotencyKey
            }
        ]);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableStepCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var checkpoint = new CheckpointWrite(
            command.InstanceId,
            aggregate.StreamVersion.Next(),
            command.StateContentType,
            [.. command.StatePayload])
        {
            DefinitionId = aggregate.DefinitionId,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId,
            DefinitionVersion = aggregate.DefinitionVersion,
            Status = WorkflowStatus.Running,
            LastStepPath = command.StepPath,
            ErrorSummary = null,
            OutcomeName = null,
            ContinueAsNewGeneration = aggregate.ContinueAsNewGeneration,
            RuntimeState = aggregate.ToCheckpointRuntimeState()
        };

        return new DurableDecision(
            [
                new WorkflowStepCompletedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    StepPath = command.StepPath
                }
            ],
            checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableYieldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StepPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StateContentType);
        ArgumentNullException.ThrowIfNull(command.StatePayload);
        if (aggregate.Status is null || aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision(
            [],
            new CheckpointWrite(
                command.InstanceId,
                aggregate.StreamVersion,
                command.StateContentType,
                [.. command.StatePayload])
            {
                DefinitionId = aggregate.DefinitionId,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId,
                DefinitionVersion = aggregate.DefinitionVersion,
                Status = WorkflowStatus.Running,
                LastStepPath = command.StepPath,
                ErrorSummary = null,
                OutcomeName = null,
                ContinueAsNewGeneration = aggregate.ContinueAsNewGeneration,
                RuntimeState = aggregate.ToCheckpointRuntimeState()
            });
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, ContinueAsNewCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StateContentType);
        ArgumentNullException.ThrowIfNull(command.StatePayload);
        if (aggregate.Status is null || aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var generation = aggregate.ContinueAsNewGeneration + 1;
        var checkpoint = new CheckpointWrite(
            command.InstanceId,
            aggregate.StreamVersion.Next(),
            command.StateContentType,
            [.. command.StatePayload])
        {
            DefinitionId = aggregate.DefinitionId,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId,
            DefinitionVersion = aggregate.DefinitionVersion,
            Status = WorkflowStatus.Running,
            LastStepPath = aggregate.LastStepPath,
            ErrorSummary = null,
            OutcomeName = null,
            ContinueAsNewGeneration = generation,
            RuntimeState = aggregate.ToContinueAsNewCheckpointRuntimeState()
        };

        return new DurableDecision(
            [
                new WorkflowContinuedAsNewEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = aggregate.ParentInstanceId,
                    RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                    PreviousStreamVersion = aggregate.StreamVersion,
                    Generation = generation
                }
            ],
            checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableStepFailedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowStepFailedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                StepPath = command.StepPath,
                ErrorSummary = command.ErrorSummary
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Failed
            }
        ], null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CancelWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>(aggregate.ExternalJobState.CreateStopRequestedEvents(
            aggregate.CreateExternalJobEventContext(command.CommandId, command.InstanceId, command.RequestedAt)));

        events.AddRange(aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt));
        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            Status = WorkflowStatus.Cancelled
        });
        return new DurableDecision(events, null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurablePauseCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.Status is null or WorkflowStatus.Paused)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowPausedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt
            }
        ]);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableResumeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.Status != WorkflowStatus.Paused)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>
        {
            new WorkflowResumedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                BufferHandling = command.BufferedDeliveries.ToString()
            }
        };

        events.AddRange(aggregate.TimerState.PlanBufferedReplay(
            DurableWorkflowAggregate.CreateTimerEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.BufferedDeliveries));

        var replayPlan = aggregate.WaitState.PlanBufferedDeliveryReplay(
            DurableWorkflowAggregate.CreateWaitEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.BufferedDeliveries);
        events.AddRange(replayPlan.Events);

        return new DurableDecision(events, null, false, replayPlan.InboxWrites);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableCompleteCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                OutcomeName = command.OutcomeName
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Completed
            }
        ], null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableFailCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Failed
            }
        ], null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, TerminateWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. aggregate.ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Terminated
            }
        ], null, true);
    }
}
