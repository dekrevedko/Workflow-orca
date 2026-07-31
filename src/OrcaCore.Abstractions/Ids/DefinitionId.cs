using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

/// <summary>
/// Identifies an immutable workflow definition family.
/// </summary>
[JsonConverter(typeof(DefinitionIdJsonConverter))]
public sealed class DefinitionId : IEquatable<DefinitionId>
{
    /// <summary>
    /// Initializes a definition identifier from a GUID value.
    /// </summary>
    private DefinitionId(Guid value)
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
    /// Parses a non-empty definition identifier from its canonical GUID text.
    /// </summary>
    public static DefinitionId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parsed = Guid.Parse(value);
        if (parsed == Guid.Empty)
        {
            throw new ArgumentException("Definition identifier cannot be empty.", nameof(value));
        }

        return new DefinitionId(parsed);
    }

    /// <summary>
    /// Attempts to parse a non-empty definition identifier from canonical GUID text.
    /// </summary>
    public static bool TryParse(string? value, out DefinitionId? definitionId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            definitionId = new DefinitionId(parsed);
            return true;
        }

        definitionId = null;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(DefinitionId? other)
    {
        return other is not null && Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DefinitionId other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    /// <summary>
    /// Compares two definition identifiers by value.
    /// </summary>
    public static bool operator ==(DefinitionId? left, DefinitionId? right)
    {
        return ReferenceEquals(left, right) || (left?.Equals(right) ?? false);
    }

    /// <summary>
    /// Compares two definition identifiers by value.
    /// </summary>
    public static bool operator !=(DefinitionId? left, DefinitionId? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the underlying GUID text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
