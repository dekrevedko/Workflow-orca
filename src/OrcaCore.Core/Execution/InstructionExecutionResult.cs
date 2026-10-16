using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal interface ICompiledInstructionExecutor
{
    ValueTask<InstructionExecutionResult> ExecuteAsync(
        CompiledInstruction instruction,
        FiberRecord fiber,
        CancellationToken cancellationToken);
}

internal abstract record InstructionExecutionResult
{
    internal sealed record Advance(InstructionId? Target = null) : InstructionExecutionResult;

    internal sealed record Suspend(
        FiberBlockedReason Reason,
        string ObligationId,
        InstructionId? ResumeAt = null) : InstructionExecutionResult;

    internal sealed record Yield : InstructionExecutionResult;

    internal sealed record BranchReturn(byte[]? ResultPayload) : InstructionExecutionResult;

    internal sealed record Fail(FiberFailure Failure) : InstructionExecutionResult;

    internal sealed record WorkflowCompleted(byte[]? ResultPayload = null) : InstructionExecutionResult;
}

internal enum FiberQuantumEndReason
{
    UserStepBudget = 0,
    Suspended = 1,
    Yielded = 2,
    BranchReturned = 3,
    Failed = 4,
    WorkflowCompleted = 5,
    InternalInstructionBudget = 6,
    ScopeStarted = 7
}

internal sealed record FiberQuantumResult(
    StructuredExecutionState State,
    FiberId FiberId,
    FiberQuantumEndReason EndReason,
    int InternalInstructionsExecuted,
    bool UserStepInvoked);
