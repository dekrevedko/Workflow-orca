using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

/// <summary>
/// Identifies one immutable version of a workflow definition.
/// </summary>
[JsonConverter(typeof(DefinitionVersionJsonConverter))]
public sealed class DefinitionVersion : IEquatable<DefinitionVersion>
{
    /// <summary>
    /// Initializes a definition version from a positive integer.
    /// </summary>
    public DefinitionVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Definition version must be positive.");
        }

        Value = value;
    }

    /// <summary>
    /// Gets the numeric definition version.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Gets the first definition version.
    /// </summary>
    public static DefinitionVersion Initial => new(1);

    /// <inheritdoc />
    public bool Equals(DefinitionVersion? other)
    {
        return other is not null && Value == other.Value;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is DefinitionVersion other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Value;
    }

    /// <summary>
    /// Compares two definition versions by value.
    /// </summary>
    public static bool operator ==(DefinitionVersion? left, DefinitionVersion? right)
    {
        return ReferenceEquals(left, right) || (left?.Equals(right) ?? false);
    }

    /// <summary>
    /// Compares two definition versions by value.
    /// </summary>
    public static bool operator !=(DefinitionVersion? left, DefinitionVersion? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns the version text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
