namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one inbound or runtime event for deduplication.
/// </summary>
public readonly record struct EventId
{
    /// <summary>
    /// Initializes an event identifier from a GUID value.
    /// </summary>
    public EventId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 event identifier.
    /// </summary>
    public static EventId New()
    {
        return new EventId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
