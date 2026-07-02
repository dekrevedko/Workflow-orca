using System.Collections.ObjectModel;

namespace OrcaCore.Core.Definitions;

internal sealed class ExecutionPointer : IEquatable<ExecutionPointer>
{
    private readonly ExecutionFrame[] frames;

    private ExecutionPointer(IEnumerable<ExecutionFrame> frames)
    {
        this.frames = frames.ToArray();
        Frames = new ReadOnlyCollection<ExecutionFrame>(this.frames);
    }

    internal static ExecutionPointer Empty { get; } = new([]);

    internal IReadOnlyList<ExecutionFrame> Frames { get; }

    internal ExecutionPointer Push(ExecutionFrame frame)
    {
        return new ExecutionPointer([.. frames, frame]);
    }

    internal ExecutionPointer Pop()
    {
        if (frames.Length == 0)
        {
            throw new InvalidOperationException("Cannot pop an empty execution pointer.");
        }

        return new ExecutionPointer(frames.Take(frames.Length - 1));
    }

    internal ExecutionPointer Clone()
    {
        return new ExecutionPointer(frames);
    }

    public bool Equals(ExecutionPointer? other)
    {
        return other is not null && frames.SequenceEqual(other.frames);
    }

    public override bool Equals(object? obj)
    {
        return obj is ExecutionPointer other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        foreach (var frame in frames)
        {
            hashCode.Add(frame);
        }

        return hashCode.ToHashCode();
    }

    public static bool operator ==(ExecutionPointer? left, ExecutionPointer? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ExecutionPointer? left, ExecutionPointer? right)
    {
        return !Equals(left, right);
    }
}

internal readonly record struct ExecutionFrame(
    string NodePath,
    int? SequenceIndex = null,
    int? LoopIteration = null,
    BranchId? BranchId = null);
