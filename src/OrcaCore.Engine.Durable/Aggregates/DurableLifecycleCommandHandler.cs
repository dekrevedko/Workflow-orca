using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableLifecycleCommandHandler
{
    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, StartWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateStartInput(command);
        if (aggregate.Status is not null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowStartedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = command.ParentInstanceId,
                RootInstanceId = command.RootInstanceId ?? command.InstanceId,
                DefinitionId = command.DefinitionId,
                DefinitionVersion = command.DefinitionVersion,
                IdempotencyKey = command.IdempotencyKey,
                DefinitionFingerprint = command.DefinitionFingerprint,
                InputFingerprint = command.InputFingerprint,
                InputContentType = command.InputContentType,
                InputPayload = command.InputPayload
            }
        ]);
    }

    private static void ValidateStartInput(StartWorkflowCommand command)
    {
        if (command.InputContentType is null && command.InputPayload is null)
        {
            return;
        }

        if (command.InputPayload is null ||
            !string.Equals(
                command.InputContentType,
            CoreWorkflowValueCodec.Format,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Durable start input must use {CoreWorkflowValueCodec.Format}.",
                nameof(command.InputContentType));
        }
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableStepCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>();
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

        var explicitReleaseKeys = command.ReleaseResourceHolderKeys
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        events.RemoveAll(workflowEvent =>
            workflowEvent is WorkflowResourcePoolReleasedEvent released &&
            explicitReleaseKeys.Contains(released.HolderKey));
        foreach (var holderKey in explicitReleaseKeys)
        {
            events.AddRange(aggregate.ResourcePoolState.CreateReleaseEvents(
                aggregate.CreateResourcePoolEventContext(
                    command.CommandId,
                    command.InstanceId,
                    command.RequestedAt),
                holderKey));
        }

        events.Add(new WorkflowStepCompletedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

        var events = new List<DurableWorkflowEvent>();
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
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            PreviousStreamVersion = aggregate.StreamVersion,
            Generation = generation
        };
        var events = new DurableWorkflowEvent[] { continued };
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

        var events = new List<DurableWorkflowEvent>();
        AddConsumeAndCancelEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds);
        if (!command.PreserveOwnership)
        {
            AddTerminalFiberCleanupEvents(
                events,
                aggregate,
                command.CommandId,
                command.InstanceId,
                command.RequestedAt,
                command.TerminalFiberIds);
        }
        events.AddRange(aggregate.SagaState.PlanCompensationForFailure(
            aggregate.CreateSagaEventContext(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt),
            command.TerminalFiberIds,
            command.FailedSagaScopeIds,
            command.CoversRootSagaEligibility,
            command.ErrorSummary));
        if (command.TerminalFiberIds.Count == 0 && !command.PreserveOwnership)
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                StepPath = command.StepPath,
                ErrorSummary = command.ErrorSummary
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableFiberFailedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>();
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
        if (command.PreserveOwnership)
        {
            events.RemoveAll(workflowEvent => workflowEvent is WorkflowResourcePoolReleasedEvent);
        }
        events.AddRange(aggregate.SagaState.PlanCompensationForFailure(
            aggregate.CreateSagaEventContext(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt),
            command.TerminalFiberIds,
            command.FailedSagaScopeIds,
            coversRootEligibility: false,
            reason: command.ErrorSummary));

        return new DurableDecision(
            events,
            CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                command.Envelope,
                command.StepPath));
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CancelWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal ||
            aggregate.Status is null or WorkflowStatus.CancellationRequested)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowCancellationRequestedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId
            }
        ]);
    }

    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        DurableTerminalLifecycleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        if (command.Status is not (WorkflowStatus.Cancelled or WorkflowStatus.Terminated))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command.Status,
                "A terminal lifecycle command must be Cancelled or Terminated.");
        }

        var events = new List<DurableWorkflowEvent>();
        AddAllOwnedCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt);
        events.RemoveAll(workflowEvent =>
            workflowEvent is WorkflowResourcePoolReleasedEvent released &&
            command.PreserveResourceHolderKeys.Contains(released.HolderKey));

        var alreadyReleased = events
            .OfType<WorkflowResourcePoolReleasedEvent>()
            .Select(workflowEvent => workflowEvent.HolderKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var cancellation in command.QueuedResourceCancellations
                     .Where(cancellation => !alreadyReleased.Contains(cancellation.HolderKey))
                     .DistinctBy(cancellation => cancellation.HolderKey, StringComparer.Ordinal))
        {
            events.Add(new WorkflowResourcePoolReleasedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                HolderKey = cancellation.HolderKey,
                Tickets = [],
                FiberId = cancellation.FiberId,
                ScopeId = cancellation.ScopeId
            });
        }

        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            Status = command.Status
        });

        var checkpoint = command.Envelope is { } envelope
            ? CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                envelope,
                aggregate.LastStepPath)
            : null;
        return new DurableDecision(events, checkpoint, true);
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

        var events = new List<DurableWorkflowEvent>
        {
            new WorkflowResumedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

        var events = new List<DurableWorkflowEvent>();
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
        if (command.PreserveOwnership)
        {
            events.RemoveAll(workflowEvent => workflowEvent is WorkflowResourcePoolReleasedEvent);
        }
        events.Add(new WorkflowCompletedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            OutcomeName = command.OutcomeName
        });
        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

        var events = new List<DurableWorkflowEvent>();
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, DurableTimeoutCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>();
        AddAllOwnedCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt);
        if (command.PreserveOwnership)
        {
            events.RemoveAll(workflowEvent => workflowEvent is WorkflowResourcePoolReleasedEvent);
        }
        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = aggregate.ParentInstanceId,
            RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
            Status = WorkflowStatus.TimedOut,
            ErrorSummary = command.ErrorSummary
        });
        return new DurableDecision(
            events,
            CreateEnvelopeCheckpoint(
                aggregate,
                command.InstanceId,
                events,
                command.Envelope,
                aggregate.LastStepPath,
                command.ErrorSummary),
            true);
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt
            }
        ]);
    }

    internal static void AddConsumeAndCancelEvents(
        List<DurableWorkflowEvent> events,
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

        foreach (var waitId in cancelWaitIds)
        {
            if (aggregate.WaitState.ActiveWaits.FirstOrDefault(wait => wait.WaitId.Equals(waitId)) is not { } wait)
            {
                continue;
            }

            events.Add(new WorkflowWaitCancelledEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
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
        List<DurableWorkflowEvent> events,
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
        List<DurableWorkflowEvent> events,
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
        IReadOnlyList<DurableWorkflowEvent> events,
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

        var events = new List<DurableWorkflowEvent>();
        AddAllOwnedCleanupEvents(
            events,
            aggregate,
            command.CommandId,
            command.InstanceId,
            command.RequestedAt);
        events.Add(new WorkflowTerminalEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Terminated
            });
        return new DurableDecision(events, null, true);
    }
}
