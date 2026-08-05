using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal sealed class FiberQuantumBudget(int maxInternalInstructions)
{
    private FiberId? activeFiberId;
    private int internalInstructions;

    public int InternalInstructions => internalInstructions;

    public bool ShouldRotate(FiberId fiberId, CompiledInstructionKind instructionKind)
    {
        Select(fiberId);
        return IsInternal(instructionKind) && internalInstructions >= maxInternalInstructions;
    }

    public void Record(FiberId fiberId, CompiledInstructionKind instructionKind)
    {
        Select(fiberId);
        if (IsInternal(instructionKind))
        {
            internalInstructions = checked(internalInstructions + 1);
        }
    }

    public void EndTurn()
    {
        activeFiberId = null;
        internalInstructions = 0;
    }

    public static bool IsInternal(CompiledInstructionKind instructionKind) =>
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
