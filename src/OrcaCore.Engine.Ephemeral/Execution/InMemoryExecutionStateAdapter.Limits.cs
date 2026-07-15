using OrcaCore.Abstractions.Errors;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private bool HandleBranchReturn(
        CompiledWorkflowPlan plan,
        StructuredExecutionState execution,
        FiberRecord fiber,
        WorkflowInstance<TState> instance)
    {
        BranchTerminalTransition returned;
        try
        {
            returned = ReturnBranch(plan, execution, fiber);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            FailExecutionBoundary(
                execution,
                fiber,
                plan.GetInstruction(fiber.InstructionId),
                instance,
                exception);
            return false;
        }

        activeExecution = returned.State;
        CancelTerminalFiberWaits(activeExecution);
        var completedScope = activeExecution.Scopes[returned.ScopeId];
        if (completedScope.Phase == ExecutionScopePhase.Failed)
        {
            FailScope(activeExecution, completedScope, instance);
            return false;
        }

        if (returned.ScopeBecameJoinable)
        {
            activeExecution = MergeAndResume(
                plan,
                activeExecution,
                activeExecution.Scopes[returned.ScopeId],
                instance);
        }

        return true;
    }

    private static void EnsureSerializedResultSize(
        CompiledWorkflowPlan plan,
        byte[] payload)
    {
        if (payload.Length > plan.CompilerOptions.MaxSerializedResultBytes)
        {
            throw new StructuredExecutionLimitException(
                StructuredExecutionLimitCodes.SerializedResultExceeded,
                $"Serialized structured result is {payload.Length} bytes, exceeding the configured " +
                $"limit of {plan.CompilerOptions.MaxSerializedResultBytes} bytes.");
        }
    }
}
