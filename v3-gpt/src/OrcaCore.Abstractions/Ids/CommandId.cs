namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one durable workflow command request.
/// </summary>
public readonly record struct CommandId
{
    /// <summary>
    /// Initializes a command identifier from a GUID value.
    /// </summary>
    public CommandId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 command identifier.
    /// </summary>
    public static CommandId New()
    {
        return new CommandId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
