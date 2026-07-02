namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one durable outbox record.
/// </summary>
public readonly record struct OutboxRecordId
{
    /// <summary>
    /// Initializes an outbox record identifier from a GUID value.
    /// </summary>
    public OutboxRecordId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 outbox record identifier.
    /// </summary>
    public static OutboxRecordId New()
    {
        return new OutboxRecordId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
