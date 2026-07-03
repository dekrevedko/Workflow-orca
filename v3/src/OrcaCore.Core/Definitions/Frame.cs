namespace OrcaCore.Core.Definitions;

/// <summary>
/// One level of an <see cref="ExecutionPointer"/> stack (CR-015): a single container's
/// position — sequence index, loop iteration counter, or branch identity. Value-equal.
/// </summary>
public readonly record struct Frame
{
    private Frame(int? sequenceIndex, int? loopIteration, BranchId? branchId)
    {
        SequenceIndex = sequenceIndex;
        LoopIteration = loopIteration;
        BranchId = branchId;
    }

    /// <summary>Position within an enclosing <see cref="SequenceNode"/>, if this frame is a sequence position.</summary>
    public int? SequenceIndex { get; }

    /// <summary>Iteration counter within an enclosing loop (<see cref="WhileNode"/>), if this frame is a loop position.</summary>
    public int? LoopIteration { get; }

    /// <summary>Identity of the enclosing <see cref="ParallelNode"/> branch, if this frame is a branch position.</summary>
    public BranchId? BranchId { get; }

    public static Frame AtSequenceIndex(int index) => new(index, null, null);

    public static Frame AtLoopIteration(int iteration) => new(null, iteration, null);

    public static Frame InBranch(BranchId branchId) => new(null, null, branchId);
}
