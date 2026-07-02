namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one logical workflow instance across activations and hosts.
/// </summary>
public readonly record struct InstanceId
{
    /// <summary>
    /// Initializes an instance identifier from a GUID value.
    /// </summary>
    public InstanceId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 instance identifier.
    /// </summary>
    public static InstanceId New()
    {
        return new InstanceId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
