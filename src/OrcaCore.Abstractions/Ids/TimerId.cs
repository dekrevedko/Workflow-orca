using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one durable timer wake-up.
/// </summary>
[JsonConverter(typeof(TimerIdJsonConverter))]
public readonly record struct TimerId
{
    /// <summary>
    /// Initializes a timer identifier from a GUID value.
    /// </summary>
    public TimerId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new time-ordered version 7 timer identifier.
    /// </summary>
    public static TimerId New()
    {
        return new TimerId(Guid.CreateVersion7());
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
