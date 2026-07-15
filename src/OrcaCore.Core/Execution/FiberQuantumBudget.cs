using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal sealed class FiberQuantumBudget(int maxInternalInstructions)
{
    private FiberId? activeFiberId;
    private int internalInstructions;

    internal int InternalInstructions => internalInstructions;

    internal bool ShouldRotate(FiberId fiberId, CompiledInstructionKind instructionKind)
    {
        Select(fiberId);
        return IsInternal(instructionKind) && internalInstructions >= maxInternalInstructions;
    }

    internal void Record(FiberId fiberId, CompiledInstructionKind instructionKind)
    {
        Select(fiberId);
        if (IsInternal(instructionKind))
        {
            internalInstructions = checked(internalInstructions + 1);
        }
    }

    internal void EndTurn()
    {
        activeFiberId = null;
        internalInstructions = 0;
    }

    internal static bool IsInternal(CompiledInstructionKind instructionKind) =>
        instructionKind != CompiledInstructionKind.Step;

    private void Select(FiberId fiberId)
    {
        if (activeFiberId == fiberId)
        {
            return;
        }

        activeFiberId = fiberId;
        internalInstructions = 0;
    }
}
