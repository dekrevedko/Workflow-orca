using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

/// <summary>
/// Identifies one runtime-owned wait registration.
/// </summary>
[JsonConverter(typeof(WaitIdJsonConverter))]
public sealed class WaitId : IEquatable<WaitId>
{
    /// <summary>
    /// Initializes a wait identifier from a GUID value.
    /// </summary>
    private WaitId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Parses a non-empty wait identifier from its canonical GUID text.
    /// </summary>
    public static WaitId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parsed = Guid.Parse(value);
        if (parsed == Guid.Empty)
        {
            throw new ArgumentException("Wait identifier cannot be empty.", nameof(value));
        }

        return new WaitId(parsed);
    }

    /// <summary>
    /// Attempts to parse a non-empty wait identifier from canonical GUID text.
    /// </summary>
    public static bool TryParse(string? value, out WaitId? waitId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            waitId = new WaitId(parsed);
            return true;
        }

        waitId = null;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(WaitId? other) => other is not null && Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WaitId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
