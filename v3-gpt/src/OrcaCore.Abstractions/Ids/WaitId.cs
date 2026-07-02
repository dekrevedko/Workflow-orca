namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one runtime-owned wait registration.
/// </summary>
public readonly record struct WaitId
{
    /// <summary>
    /// Initializes a wait identifier from a GUID value.
    /// </summary>
    public WaitId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 wait identifier.
    /// </summary>
    public static WaitId New()
    {
        return new WaitId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
