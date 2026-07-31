using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore;

/// <summary>
/// Identifies one logical workflow instance across activations and hosts.
/// </summary>
[JsonConverter(typeof(InstanceIdJsonConverter))]
public sealed class InstanceId : IEquatable<InstanceId>
{
    /// <summary>
    /// Initializes an instance identifier from a GUID value.
    /// </summary>
    private InstanceId(Guid value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the underlying globally unique value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Parses a non-empty instance identifier from its canonical GUID text.
    /// </summary>
    public static InstanceId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var parsed = Guid.Parse(value);
        if (parsed == Guid.Empty)
        {
            throw new ArgumentException("Instance identifier cannot be empty.", nameof(value));
        }

        return new InstanceId(parsed);
    }

    /// <summary>
    /// Attempts to parse a non-empty instance identifier from canonical GUID text.
    /// </summary>
    public static bool TryParse(string? value, out InstanceId? instanceId)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            instanceId = new InstanceId(parsed);
            return true;
        }

        instanceId = null;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(InstanceId? other) => other is not null && Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InstanceId other && Equals(other);

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
