using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies a zero-based durable stream version.
/// </summary>
[JsonConverter(typeof(StreamVersionJsonConverter))]
public readonly record struct StreamVersion
{
    /// <summary>
    /// Initializes a stream version from a non-negative value.
    /// </summary>
    public StreamVersion(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Stream version cannot be negative.");
        }

        Value = value;
    }

    /// <summary>
    /// Gets the numeric stream version.
    /// </summary>
    public long Value { get; }

    /// <summary>
    /// Gets the version before any events have been appended.
    /// </summary>
    public static StreamVersion Empty => new(0);

    /// <summary>
    /// Returns the next stream version.
    /// </summary>
    public StreamVersion Next()
    {
        return new StreamVersion(Value + 1);
    }

    /// <summary>
    /// Returns the version text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
