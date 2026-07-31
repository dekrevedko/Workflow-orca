using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<BranchReturnCommit> CommitBranchReturnAsync(
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
        if (FindScopedLease(fiber, ownedObligations) is
            {
                LeasePhase: nameof(DurableLeaseObligationPhase.Held)
            } &&
            instruction.NextInstructionId is { } releaseId)
        {
            var resultPayload = ProjectBranchResultPayload(execution, fiber);
            var stagedReturn = ClearResume(fiber) with
            {
                InstructionId = releaseId,
                ResultPayload = resultPayload
            };
            execution = execution with
            {
                Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                {
                    [fiber.Id] = stagedReturn
                },
                Scheduler = FiberScheduler.CompleteTurn(
                    execution.Scheduler,
                    fiber.Id,
                    requeueSelected: true)
            };
            RemoveConsumedObligation(ownedObligations, consumedWaitId);
            var staged = await context.Processor.ProcessAsync(
                new DurableStepCompletedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    $"{instruction.Path}:branch-return-pending-release",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                },
                cancellationToken).ConfigureAwait(false);
            return staged.Outcome == DurableCommandOutcome.Committed
                ? new BranchReturnCommit(execution, staged.StreamVersion, null)
                : new BranchReturnCommit(execution, currentVersion, Conflict(staged));
        }

        var returningFibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [fiber.Id] = ClearResume(fiber)
        };
        execution = execution with { Fibers = returningFibers };
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        try
        {
            execution = ReturnBranch(execution, fiber).State;
        }
        catch (StructuredExecutionLimitException exception)
        {
            var terminal = await FailBranchReturnLimitAsync(
                context,
                execution,
                state,
                fiber,
                instruction,
                ownedObligations,
                consumedWaitId,
                currentVersion,
                exception,
                cancellationToken).ConfigureAwait(false);
            return new BranchReturnCommit(execution, currentVersion, terminal);
        }

        var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
        var committed = await context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{instruction.Path}:branch-return",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion,
                ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                CancelWaitIds = cleanup.WaitIds,
                CancelTimerIds = cleanup.TimerIds,
                TerminalFiberIds = cleanup.TerminalFiberIds
            },
            cancellationToken).ConfigureAwait(false);
        return committed.Outcome == DurableCommandOutcome.Committed
            ? new BranchReturnCommit(execution, committed.StreamVersion, null)
            : new BranchReturnCommit(execution, currentVersion, Conflict(committed));
    }

    private void EnsureSerializedResultSize(byte[] payload)
    {
        if (payload.Length > plan.CompilerOptions.MaxSerializedResultBytes)
        {
            throw new StructuredExecutionLimitException(
                StructuredExecutionLimitCodes.SerializedResultExceeded,
                $"Serialized structured result is {payload.Length} bytes, exceeding the configured " +
                $"limit of {plan.CompilerOptions.MaxSerializedResultBytes} bytes.");
        }
    }

    private async Task<DurableSegmentResult> FailBranchReturnLimitAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        FiberRecord fiber,
        CompiledInstruction instruction,
        List<DurableOwnedObligationState> ownedObligations,
        WaitId? consumedWaitId,
        StreamVersion currentVersion,
        StructuredExecutionLimitException exception,
        CancellationToken cancellationToken)
    {
        var failure = FailureProvenance.Create(
            plan,
            execution,
            fiber,
            instruction,
            exception.Code,
            exception.Message);
        execution = FailFiberAndAncestors(execution, fiber, failure);
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
        var failedReturn = await context.Processor.ProcessAsync(
            new DurableStepFailedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{instruction.Path}:branch-return",
                $"{failure.Code}: {failure.Message}",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion,
                ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                CancelWaitIds = cleanup.WaitIds,
                CancelTimerIds = cleanup.TimerIds,
                TerminalFiberIds = cleanup.TerminalFiberIds,
                FailedSagaScopeIds = FailedSagaScopes(execution),
                CoversRootSagaEligibility = RootFailed(execution)
            },
            cancellationToken).ConfigureAwait(false);
        return failedReturn.Outcome == DurableCommandOutcome.Committed
            ? DurableSegmentResult.Terminal
            : Conflict(failedReturn);
    }

    private sealed record BranchReturnCommit(
        StructuredExecutionState Execution,
        StreamVersion StreamVersion,
        DurableSegmentResult? Terminal);
}
