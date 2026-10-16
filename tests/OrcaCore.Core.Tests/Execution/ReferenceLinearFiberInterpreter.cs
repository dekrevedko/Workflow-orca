using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal static class ReferenceLinearFiberInterpreter
{
    internal static async ValueTask<FiberQuantumResult> RunQuantumAsync(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        ICompiledInstructionExecutor executor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(executor);

        var selected = FiberScheduler.SelectNext(state.Scheduler) ??
            throw new InvalidOperationException("The execution state has no runnable fiber.");
        if (!state.Fibers.TryGetValue(selected, out var fiber) || fiber.Phase != FiberPhase.Runnable)
        {
            throw new InvalidOperationException(
                $"Scheduled fiber '{selected}' is missing or is not runnable.");
        }

        var instructionIndexes = plan.Instructions
            .Select((instruction, index) => (instruction.Id, index))
            .ToDictionary(candidate => candidate.Id, candidate => candidate.index);
        var quantumBudget = new FiberQuantumBudget(
            plan.CompilerOptions.MaxInternalInstructionsPerQuantum);
        var userStepInvoked = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!instructionIndexes.TryGetValue(fiber.InstructionId, out var instructionIndex))
            {
                throw new InvalidOperationException(
                    $"Instruction '{fiber.InstructionId}' is not present in compiled plan '{plan.Fingerprint}'.");
            }

            var instruction = plan.Instructions[instructionIndex];
            var isUserStep = !FiberQuantumBudget.IsInternal(instruction.Kind);
            if (isUserStep && userStepInvoked)
            {
                return Finish(
                    state,
                    fiber,
                    FiberQuantumEndReason.UserStepBudget,
                    quantumBudget.InternalInstructions,
                    userStepInvoked,
                    requeue: true);
            }

            if (quantumBudget.ShouldRotate(fiber.Id, instruction.Kind))
            {
                fiber = fiber with
                {
                    ForcedRotationCount = checked(fiber.ForcedRotationCount + 1)
                };
                return Finish(
                    state,
                    fiber,
                    FiberQuantumEndReason.InternalInstructionBudget,
                    quantumBudget.InternalInstructions,
                    userStepInvoked,
                    requeue: true);
            }

            quantumBudget.Record(fiber.Id, instruction.Kind);

            if (instruction.Kind == CompiledInstructionKind.StartScope)
            {
                var scopePlanId = new ScopePlanId($"scope:{instruction.Path}");
                var scopePlan = plan.Scopes.SingleOrDefault(candidate => candidate.Id == scopePlanId) ??
                    throw new InvalidOperationException(
                        $"Scope-start instruction '{instruction.Id}' has no scope plan '{scopePlanId}'.");
                var scopeTransition = ScopeReducer.StartScope(state, fiber.Id, scopePlan);
                return new FiberQuantumResult(
                    scopeTransition.State,
                    fiber.Id,
                    FiberQuantumEndReason.ScopeStarted,
                    quantumBudget.InternalInstructions,
                    userStepInvoked);
            }

            var execution = await executor.ExecuteAsync(instruction, fiber, cancellationToken)
                .ConfigureAwait(false);
            if (isUserStep)
            {
                userStepInvoked = true;
            }

            switch (execution)
            {
                case InstructionExecutionResult.Advance advance:
                    var nextInstruction = advance.Target ??
                        instruction.NextInstructionId ??
                        NextInstruction(plan, instructionIndex);
                    fiber = fiber with { InstructionId = nextInstruction };
                    break;
                case InstructionExecutionResult.Suspend suspend:
                    if (suspend.ResumeAt is { } resumeAt)
                    {
                        fiber = fiber with { InstructionId = resumeAt };
                    }

                    fiber = FiberReducer.Block(fiber, suspend.Reason, suspend.ObligationId);
                    return Finish(
                        state,
                        fiber,
                        FiberQuantumEndReason.Suspended,
                        quantumBudget.InternalInstructions,
                        userStepInvoked,
                        requeue: false);
                case InstructionExecutionResult.Yield:
                fiber = fiber with
                {
                    QuantumRotationCount = checked(fiber.QuantumRotationCount + 1)
                };
                    return Finish(
                        state,
                        fiber,
                        FiberQuantumEndReason.Yielded,
                        quantumBudget.InternalInstructions,
                        userStepInvoked,
                        requeue: true);
                case InstructionExecutionResult.BranchReturn branchReturn:
                    var branchTransition = ScopeReducer.RecordBranchReturn(
                        state,
                        fiber,
                        branchReturn.ResultPayload);
                    return new FiberQuantumResult(
                        branchTransition.State,
                        fiber.Id,
                        FiberQuantumEndReason.BranchReturned,
                        quantumBudget.InternalInstructions,
                        userStepInvoked);
                case InstructionExecutionResult.Fail failed:
                    fiber = FiberReducer.Fail(fiber, failed.Failure);
                    return Finish(
                        state,
                        fiber,
                        FiberQuantumEndReason.Failed,
                        quantumBudget.InternalInstructions,
                        userStepInvoked,
                        requeue: false);
                case InstructionExecutionResult.WorkflowCompleted completed:
                    if (fiber.Id != state.RootFiberId)
                    {
                        throw new InvalidOperationException(
                            $"Only root fiber '{state.RootFiberId}' may complete the workflow.");
                    }

                    fiber = FiberReducer.Complete(fiber, completed.ResultPayload);
                    return Finish(
                        state,
                        fiber,
                        FiberQuantumEndReason.WorkflowCompleted,
                        quantumBudget.InternalInstructions,
                        userStepInvoked,
                        requeue: false);
                default:
                    throw new NotSupportedException(
                        $"Instruction result '{execution.GetType().Name}' is not implemented yet.");
            }
        }
    }

    private static InstructionId NextInstruction(CompiledWorkflowPlan plan, int currentIndex)
    {
        if (currentIndex + 1 >= plan.Instructions.Count)
        {
            throw new InvalidOperationException("The compiled instruction has no continuation target.");
        }

        return plan.Instructions[currentIndex + 1].Id;
    }

    private static FiberQuantumResult Finish(
        StructuredExecutionState state,
        FiberRecord fiber,
        FiberQuantumEndReason endReason,
        int internalInstructions,
        bool userStepInvoked,
        bool requeue)
    {
        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
        {
            [fiber.Id] = fiber
        };
        var scheduler = FiberScheduler.CompleteTurn(
            state.Scheduler,
            fiber.Id,
            requeue);
        return new FiberQuantumResult(
            state with { Fibers = fibers, Scheduler = scheduler },
            fiber.Id,
            endReason,
            internalInstructions,
            userStepInvoked);
    }
}
