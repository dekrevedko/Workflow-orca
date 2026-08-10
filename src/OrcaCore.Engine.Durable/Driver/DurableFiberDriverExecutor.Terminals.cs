using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<DurableSegmentResult> ContinueAsNewAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        if (!IsQuiescentRootRollover(execution, fiber, ownedObligations))
        {
            var rejected = await context.Processor.ProcessAsync(
                new DurableFailCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    "SFE-RUN-001: ContinueAsNewRequiresQuiescentRoot.",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    PreserveOwnership = true
                },
                cancellationToken).ConfigureAwait(false);
            return rejected.Outcome == DurableCommandOutcome.Committed
                ? DurableSegmentResult.Terminal
                : Conflict(rejected);
        }

        if (instruction.Operation is not Func<TState, TState> stateSelector)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                $"Compiled ContinueAsNew '{instruction.Path}' has no typed state selector.");
        }

        state = stateSelector(state);
        var nextGeneration = checked(execution.ContinueAsNewGeneration + 1);
        var workflowDeadline = execution.WorkflowDeadline;
        var workflowDeadlineTimerId = execution.WorkflowDeadlineTimerId;
        var generationStart = plan.Instructions[0].Kind == CompiledInstructionKind.Init
            ? RequiredNext(plan.Instructions[0])
            : plan.Instructions[0].Id;
        execution = StructuredExecutionState.Create(
            context.InstanceId,
            nextGeneration,
            generationStart) with
        {
            WorkflowDeadline = workflowDeadline,
            WorkflowDeadlineTimerId = workflowDeadlineTimerId
        };
        var serialized = context.Serializer.Serialize(state);
        var rollover = await context.Processor.ProcessAsync(
            new ContinueAsNewCommand
            {
                CommandId = CommandId.New(),
                InstanceId = context.InstanceId,
                RequestedAt = context.TimeProvider.GetUtcNow(),
                StateContentType = serialized.ContentType,
                StatePayload = serialized.Payload,
                Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken).ConfigureAwait(false);
        return rollover.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.ContinuedAsNew
            : Conflict(rollover);
    }

    private async Task<DurableSegmentResult> TimeoutWorkflowAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        QuarantineCapacityReservations(ownedObligations);
        var deadline = execution.WorkflowDeadline ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                "Workflow timeout was requested without a persisted deadline.");
        var exception = global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.WorkflowDeadline(deadline);
        var timedOut = await context.Processor.ProcessAsync(
            new DurableTimeoutCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{exception.Code}: {exception.Message}",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion,
                PreserveOwnership = HasQuarantinedCapacity(ownedObligations)
            },
            CancellationToken.None).ConfigureAwait(false);
        return timedOut.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.Terminal
            : Conflict(timedOut);
    }

    private async Task<DurableSegmentResult> CompleteAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        List<DurableOwnedObligationState> ownedObligations,
        WaitId? consumedWaitId,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        DurableWorkflowOutputState? output = null;
        if (instruction.OutputSelector is { } outputSelector &&
            instruction.OutputType is { } outputType &&
            instruction.OutputSchemaIdentity is { } outputSchemaIdentity)
        {
            var projected = StructuredInvocationCache.Invoke(outputSelector, state);
            var serialized = codec.Serialize(projected, outputType, outputSchemaIdentity);
            output = new DurableWorkflowOutputState
            {
                TypeName = outputType.AssemblyQualifiedName ?? outputType.FullName ?? outputType.Name,
                SchemaIdentity = serialized.SchemaIdentity,
                Payload = serialized.Payload
            };
        }

        var completedFiber = FiberReducer.Complete(ClearResume(fiber));
        execution = execution with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
            {
                [fiber.Id] = completedFiber
            },
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
        };
        var end = definition.RootSequence.Children
            .OfType<EndNode<TState>>()
            .Single(node => node.NodeId == instruction.Path);
        var outcomeName = instruction.FixedOutcomeName ?? end.ResolveOutcome(state);
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
        var completion = await context.Processor.ProcessAsync(
            new DurableCompleteCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                outcomeName,
                BuildEnvelope(context, execution, state, ownedObligations, output))
            {
                ExpectedStreamVersion = currentVersion,
                ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                CancelWaitIds = cleanup.WaitIds,
                CancelTimerIds = cleanup.TimerIds,
                TerminalFiberIds = cleanup.TerminalFiberIds,
                PreserveOwnership = HasQuarantinedCapacity(ownedObligations)
            },
            cancellationToken).ConfigureAwait(false);
        return completion.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.Terminal
            : Conflict(completion);
    }

    private static void QuarantineCapacityReservations(
        IList<DurableOwnedObligationState> ownedObligations)
    {
        for (var index = 0; index < ownedObligations.Count; index++)
        {
            var obligation = ownedObligations[index];
            if (obligation.Kind == DurableOwnedObligationKind.Resource &&
                obligation.ProtectionToken is not null &&
                obligation.LeasePhase is
                    nameof(DurableLeaseObligationPhase.PendingCommit) or
                    nameof(DurableLeaseObligationPhase.Held) or
                    nameof(DurableLeaseObligationPhase.ReviewMarked) or
                    nameof(DurableLeaseObligationPhase.AmbiguousHeld))
            {
                ownedObligations[index] = obligation with
                {
                    LeasePhase = nameof(DurableLeaseObligationPhase.Quarantined)
                };
            }
        }
    }

    private static bool HasQuarantinedCapacity(
        IEnumerable<DurableOwnedObligationState> ownedObligations) =>
        ownedObligations.Any(obligation =>
            obligation.Kind == DurableOwnedObligationKind.Resource &&
            obligation.ProtectionToken is not null &&
            obligation.LeasePhase is
                nameof(DurableLeaseObligationPhase.Quarantined) or
                nameof(DurableLeaseObligationPhase.LeaseLost));
}
