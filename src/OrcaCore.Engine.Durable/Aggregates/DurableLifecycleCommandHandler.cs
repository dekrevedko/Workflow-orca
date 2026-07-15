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
                IdempotencyKey = command.IdempotencyKey,
                InputContentType = command.InputContentType,
                InputPayload = command.InputPayload
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

        var events = new List<WorkflowEvent>();
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds);
        AddTerminalFiberCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.TerminalFiberIds);
        foreach (var transfer in command.SagaScopeTransfers)
        {
            events.Add(new SagaForwardActionsTransferredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                FromExecutionScopeId = transfer.FromScopeId,
                ToExecutionScopeId = transfer.ToScopeId
            });
        }

        events.Add(new WorkflowStepCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            StepPath = command.StepPath
        });

        return new DurableDecision(
            events,
            CreateEnvelopeCheckpoint(aggregate, command.InstanceId, events, command.Envelope, command.StepPath));
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableYieldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StepPath);
        ArgumentNullException.ThrowIfNull(command.Envelope);
        if (aggregate.Status is null || aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            [],
            []);

        return new DurableDecision(
            events,
            CreateEnvelopeCheckpoint(aggregate, command.InstanceId, events, command.Envelope, command.StepPath));
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
        var continued = new WorkflowContinuedAsNewEvent
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
        };
        var events = new WorkflowEvent[] { continued };
        var checkpoint = command.Envelope is { } envelope
            ? CreateEnvelopeCheckpoint(aggregate, command.InstanceId, events, envelope, lastStepPath: null)
            : new CheckpointWrite(
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

        return new DurableDecision(events, checkpoint);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableStepFailedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds);
        AddTerminalFiberCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.TerminalFiberIds);
        events.AddRange(aggregate.SagaState.PlanCompensationForFailure(
            aggregate.CreateSagaEventContext(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt),
            command.TerminalFiberIds,
            command.FailedSagaScopeIds,
            command.CoversRootSagaEligibility,
            command.ErrorSummary));
        if (command.TerminalFiberIds.Count == 0)
        {
            AddAllOwnedCleanupEvents(
                events,
                aggregate,
                command.CommandId,
                command.InstanceId,
                command.RequestedAt);
        }
        events.AddRange(
        [
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
        ]);

        var checkpoint = command.Envelope is { } envelope
            ? CreateEnvelopeCheckpoint(aggregate, command.InstanceId, events, envelope, command.StepPath)
            : null;
        return new DurableDecision(events, checkpoint, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CancelWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        AddAllOwnedCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt);
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

        var events = new List<WorkflowEvent>();
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds);
        AddTerminalFiberCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.TerminalFiberIds);
        if (command.TerminalFiberIds.Count == 0)
        {
            AddAllOwnedCleanupEvents(
                events,
                aggregate,
                command.CommandId,
                command.InstanceId,
                command.RequestedAt);
        }
        events.Add(new WorkflowCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            OutcomeName = command.OutcomeName
        });
        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            Status = WorkflowStatus.Completed
        });

        var checkpoint = command.Envelope is { } envelope
            ? CreateEnvelopeCheckpoint(aggregate, command.InstanceId, events, envelope, aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableFailCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        if (!command.PreserveOwnership)
        {
            AddAllOwnedCleanupEvents(
                events,
                aggregate,
                command.CommandId,
                command.InstanceId,
                command.RequestedAt);
        }
        events.Add(
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Failed
            });

        var checkpoint = command.Envelope is { } envelope
            ? CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath,
                command.ErrorSummary)
            : null;
        return new DurableDecision(events, checkpoint, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableParkCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.Status is null or WorkflowStatus.Parked)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowParkedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Reason = command.Reason,
                ErrorSummary = command.ErrorSummary,
                FailedAttemptCount = command.FailedAttemptCount,
                PositionStreamVersion = command.PositionStreamVersion
            }
        ], null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableUnparkCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.Status != WorkflowStatus.Parked)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowUnparkedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt
            }
        ]);
    }

    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        DurableContinuationAttemptFailedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.Status is null or WorkflowStatus.Parked)
        {
            return DurableDecision.Empty;
        }

        var samePosition = aggregate.ContinuationFailurePositionStreamVersion == command.PositionStreamVersion;
        var attemptCount = samePosition ? aggregate.ContinuationFailureCount + 1 : 1;
        return new DurableDecision([
            new WorkflowContinuationAttemptFailedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                AttemptCount = attemptCount,
                PositionStreamVersion = command.PositionStreamVersion,
                NextEligibleAt = command.NextEligibleAt,
                ErrorSummary = command.ErrorSummary
            }
        ]);
    }

    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        DurableContinuationAttemptResetCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.ContinuationFailureCount == 0 ||
            aggregate.IsTerminal ||
            aggregate.Status == WorkflowStatus.Parked)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowContinuationAttemptResetEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt
            }
        ]);
    }

    internal static void AddConsumeAndCancelEvents(
        List<WorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        IReadOnlyList<WaitId> consumedResumeWaitIds,
        IReadOnlyList<WaitId> cancelWaitIds,
        IReadOnlyList<TimerId> cancelTimerIds)
    {
        foreach (var waitId in consumedResumeWaitIds)
        {
            if (aggregate.WaitState.FindPendingResume(waitId) is not { } pending)
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
                WaitId = waitId,
                FiberId = pending.FiberId,
                ScopeId = pending.ScopeId
            });
        }

        foreach (var waitId in cancelWaitIds)
        {
            if (aggregate.WaitState.ActiveWaits.FirstOrDefault(wait => wait.WaitId == waitId) is not { } wait)
            {
                continue;
            }

            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(commandId),
                OccurredAt = requestedAt,
                WaitId = waitId,
                FiberId = wait.FiberId,
                ScopeId = wait.ScopeId
            });
        }

        foreach (var timerId in cancelTimerIds)
        {
            if (aggregate.TimerState.FindActive(timerId) is not { } timer)
            {
                continue;
            }

            events.Add(new WorkflowTimerCancelledEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(commandId),
                OccurredAt = requestedAt,
                TimerId = timerId,
                FiberId = timer.FiberId,
                ScopeId = timer.ScopeId
            });
        }
    }

    private static void AddTerminalFiberCleanupEvents(
        List<WorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        IReadOnlyList<FiberId> terminalFiberIds)
    {
        if (terminalFiberIds.Count == 0)
        {
            return;
        }

        var owners = terminalFiberIds.ToHashSet();
        var cancelledWaitIds = events
            .OfType<WorkflowWaitCancelledEvent>()
            .Select(workflowEvent => workflowEvent.WaitId)
            .ToHashSet();
        var cancelledTimerIds = events
            .OfType<WorkflowTimerCancelledEvent>()
            .Select(workflowEvent => workflowEvent.TimerId)
            .ToHashSet();
        var ownedWaitIds = aggregate.WaitState.ActiveWaits
            .Where(wait => wait.FiberId is { } fiberId && owners.Contains(fiberId))
            .Select(wait => wait.WaitId)
            .Where(waitId => !cancelledWaitIds.Contains(waitId))
            .ToArray();
        var ownedTimerIds = aggregate.TimerState.ActiveTimers
            .Where(timer => timer.FiberId is { } fiberId && owners.Contains(fiberId))
            .Select(timer => timer.TimerId)
            .Where(timerId => !cancelledTimerIds.Contains(timerId))
            .ToArray();

        events.AddRange(aggregate.ChildState.CreateCancellationEvents(
            aggregate.CreateChildWorkflowEventContext(commandId, instanceId, requestedAt),
            owners));
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            commandId,
            instanceId,
            requestedAt,
            [],
            ownedWaitIds,
            ownedTimerIds);
        events.AddRange(aggregate.ExternalJobState.CreateStopRequestedEvents(
            aggregate.CreateExternalJobEventContext(commandId, instanceId, requestedAt),
            owners));
        events.AddRange(aggregate.ResourcePoolState.CreateReleaseEvents(
            aggregate.CreateResourcePoolEventContext(commandId, instanceId, requestedAt),
            ownerFiberIds: owners));
    }

    private static void AddAllOwnedCleanupEvents(
        List<WorkflowEvent> events,
        DurableWorkflowAggregate aggregate,
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        events.AddRange(aggregate.ChildState.CreateCancellationEvents(
            aggregate.CreateChildWorkflowEventContext(commandId, instanceId, requestedAt)));
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            commandId,
            instanceId,
            requestedAt,
            [],
            aggregate.WaitState.ActiveWaits.Select(wait => wait.WaitId).ToArray(),
            aggregate.TimerState.ActiveTimers.Select(timer => timer.TimerId).ToArray());
        events.AddRange(aggregate.ExternalJobState.CreateStopRequestedEvents(
            aggregate.CreateExternalJobEventContext(commandId, instanceId, requestedAt)));
        events.AddRange(aggregate.ReleaseEvents(commandId, instanceId, requestedAt));
    }

    /// <summary>
    /// Creates the driver checkpoint for one advancement commit: the versioned envelope becomes
    /// the payload, and runtime state reflects the decision's events as if already committed.
    /// </summary>
    internal static CheckpointWrite CreateEnvelopeCheckpoint(
        DurableWorkflowAggregate aggregate,
        InstanceId instanceId,
        IReadOnlyList<WorkflowEvent> events,
        DurableCheckpointPayload envelope,
        string? lastStepPath,
        string? errorSummary = null)
    {
        var projected = aggregate.ProjectEvents(events);
        DurableWorkflowReplayApplier.ApplyStructuredEnvelopeStatus(projected, envelope);
        return new CheckpointWrite(
            instanceId,
            projected.StreamVersion,
            envelope.ContentType,
            envelope.Payload)
        {
            DefinitionId = projected.DefinitionId,
            ParentInstanceId = projected.ParentInstanceId,
            RootInstanceId = projected.RootInstanceId,
            DefinitionVersion = projected.DefinitionVersion,
            Status = projected.Status ?? WorkflowStatus.Running,
            LastStepPath = lastStepPath,
            ErrorSummary = errorSummary ?? projected.ErrorSummary,
            OutcomeName = projected.OutcomeName,
            ContinueAsNewGeneration = projected.ContinueAsNewGeneration,
            RuntimeState = projected.ToCheckpointRuntimeState()
        };
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, TerminateWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>();
        AddAllOwnedCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt);
        events.Add(new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Terminated
            });
        return new DurableDecision(events, null, true);
    }
}
