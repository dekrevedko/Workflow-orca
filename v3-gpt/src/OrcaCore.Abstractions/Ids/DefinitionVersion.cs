using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Identifies one immutable version of a workflow definition.
/// </summary>
[JsonConverter(typeof(DefinitionVersionJsonConverter))]
public readonly record struct DefinitionVersion
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

    /// <summary>
    /// Returns the version text.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString();
    }
}
