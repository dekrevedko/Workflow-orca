namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Monotonic position within a single instance's durable event stream (DU-010/DU-011).
/// Zero denotes the empty stream (no events appended yet); each appended event occupies the
/// next value. Wraps <see cref="long"/> because long-lived instances can accumulate large
/// stream positions over their lifetime.
/// </summary>
public readonly record struct StreamVersion(long Value)
{
    public long Value { get; } = Value >= 0
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), Value, "StreamVersion must not be negative.");

    public override string ToString() => Value.ToString();
}
