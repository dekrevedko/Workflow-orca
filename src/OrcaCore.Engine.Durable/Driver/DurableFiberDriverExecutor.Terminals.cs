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
            throw new WorkflowDefinitionException(
                $"Compiled ContinueAsNew '{instruction.Path}' has no typed state selector.");
        }

        state = stateSelector(state);
        var nextGeneration = checked(execution.ContinueAsNewGeneration + 1);
        var generationStart = plan.Instructions[0].Kind == CompiledInstructionKind.Init
            ? RequiredNext(plan.Instructions[0])
            : plan.Instructions[0].Id;
        execution = StructuredExecutionState.Create(
            context.InstanceId,
            nextGeneration,
            generationStart);
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
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
        var completion = await context.Processor.ProcessAsync(
            new DurableCompleteCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                end.ResolveOutcome(state),
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion,
                ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                CancelWaitIds = cleanup.WaitIds,
                CancelTimerIds = cleanup.TimerIds,
                TerminalFiberIds = cleanup.TerminalFiberIds
            },
            cancellationToken).ConfigureAwait(false);
        return completion.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.Terminal
            : Conflict(completion);
    }
}
