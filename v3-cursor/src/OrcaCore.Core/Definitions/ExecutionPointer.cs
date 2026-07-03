namespace OrcaCore.Core.Definitions;

/// <summary>
/// Execution position in the definition graph as a call-stack of <see cref="Frame"/>s
/// (CR-015), not a flat index, so nested structures (branch inside <see cref="ParallelNode"/>
/// inside <see cref="IfNode"/>) and rehydration are represented exactly. Immutable and
/// value-equal: every mutation returns a new pointer.
/// </summary>
public sealed record ExecutionPointer
{
    private ExecutionPointer(IReadOnlyList<Frame> frames)
    {
        Frames = frames;
    }

    /// <summary>The empty pointer — no position pushed yet.</summary>
    public static ExecutionPointer Empty { get; } = new([]);

    /// <summary>Frames from outermost (index 0) to innermost (current position).</summary>
    public IReadOnlyList<Frame> Frames { get; }

    /// <summary>Returns a new pointer with <paramref name="frame"/> pushed on top.</summary>
    public ExecutionPointer Push(Frame frame) => new([.. Frames, frame]);

    /// <summary>Returns a new pointer with the innermost frame removed.</summary>
    /// <exception cref="InvalidOperationException">The pointer has no frames to pop.</exception>
    public ExecutionPointer Pop() => Frames.Count switch
    {
        0 => throw new InvalidOperationException("Cannot pop an empty ExecutionPointer."),
        _ => new ExecutionPointer(Frames.Take(Frames.Count - 1).ToArray()),
    };

    /// <summary>Returns a value-equal copy of this pointer.</summary>
    public ExecutionPointer Copy() => new([.. Frames]);

    public bool Equals(ExecutionPointer? other) =>
        other is not null && Frames.SequenceEqual(other.Frames);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var frame in Frames)
        {
            hash.Add(frame);
        }

        return hash.ToHashCode();
    }
}
