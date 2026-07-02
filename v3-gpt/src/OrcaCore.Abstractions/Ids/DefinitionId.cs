namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies an immutable workflow definition family.
/// </summary>
public readonly record struct DefinitionId
{
    /// <summary>
    /// Initializes a definition identifier from a GUID value.
    /// </summary>
    public DefinitionId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 definition identifier.
    /// </summary>
    public static DefinitionId New()
    {
        return new DefinitionId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
