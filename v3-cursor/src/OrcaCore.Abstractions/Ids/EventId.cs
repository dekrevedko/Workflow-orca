namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Deduplication identity for an inbound event envelope (EV-001).
/// </summary>
public readonly record struct EventId(Guid Value)
{
    /// <summary>
    /// Creates a new time-ordered identifier using UUID version 7.
    /// </summary>
    public static EventId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
