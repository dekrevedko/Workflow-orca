using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies the durable causal chain that produced workflow engine facts.
/// </summary>
[JsonConverter(typeof(CausationIdJsonConverter))]
public readonly record struct CausationId
{
    /// <summary>
    /// Initializes a causation identifier from a GUID value.
    /// </summary>
    public CausationId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 causation identifier.
    /// </summary>
    public static CausationId New()
    {
        return new CausationId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
