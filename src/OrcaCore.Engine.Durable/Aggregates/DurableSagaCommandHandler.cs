using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Aggregates;

internal static class DurableSagaCommandHandler
{
    internal static DurableDecision Handle(
        DurableWorkflowAggregate aggregate,
        RecordSagaForwardActionCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.SagaState.HasForwardAction(command.ScopeId, command.ActionKey))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaForwardActionCompletedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                CompensationKey = command.CompensationKey,
                FiberId = command.FiberId,
                OwningScopeId = command.OwningScopeId,
                InstructionId = command.InstructionId,
                CommittedSequence = command.CommittedSequence,
                CanonicalBranchOrder = command.CanonicalBranchOrder,
                CanonicalInstructionOrder = command.CanonicalInstructionOrder,
                ScopeOrderOverride = command.ScopeOrderOverride
            }
        ]);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, RequestSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal || aggregate.SagaState.HasRequestedCompensation(command.ScopeId))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision(aggregate.SagaState.PlanCompensation(
            aggregate.CreateSagaEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.ScopeId,
            command.Reason));
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, SagaForwardActionTimedOutCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>
        {
            new SagaForwardActionTimedOutEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                CompensateScope = command.CompensateScope
            }
        };

        if (command.CompensateScope && !aggregate.SagaState.HasRequestedCompensation(command.ScopeId))
        {
            events.AddRange(aggregate.SagaState.PlanCompensation(
                aggregate.CreateSagaEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
                command.ScopeId,
                "timeout"));
        }

        return new DurableDecision(events);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, CompleteSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<DurableWorkflowEvent>
        {
            new SagaCompensationCompletedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey
            }
        };

        if (aggregate.SagaState.AllCompensationsCompleteAfter(command.ScopeId, command.ActionKey))
        {
            events.Add(new WorkflowTerminalEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                Status = WorkflowStatus.Compensated
            });
        }

        return new DurableDecision(events, null, events.Any(workflowEvent => workflowEvent is WorkflowTerminalEvent));
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, FailSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaCompensationFailedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                ErrorSummary = command.ErrorSummary
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                Status = WorkflowStatus.CompensationFailed
            }
        ], null, true);
    }

    internal static DurableDecision Handle(DurableWorkflowAggregate aggregate, RecordSagaManualRecoveryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (aggregate.Status is not WorkflowStatus.CompensationFailed)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaManualRecoveryRecordedEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                OperatorId = command.OperatorId,
                RecoveryAction = command.RecoveryAction,
                Reason = command.Reason,
                TargetStatus = command.TargetStatus
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = DurableWorkflowAggregate.ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = aggregate.ParentInstanceId,
                RootInstanceId = aggregate.RootInstanceId ?? aggregate.InstanceId,
                Status = command.TargetStatus
            }
        ], null, true);
    }
}
