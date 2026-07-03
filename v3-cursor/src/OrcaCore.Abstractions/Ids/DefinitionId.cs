namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Stable identity for an authored workflow definition (CR-022).
/// </summary>
public readonly record struct DefinitionId(Guid Value)
{
    /// <summary>
    /// Creates a new time-ordered identifier using UUID version 7.
    /// </summary>
    public static DefinitionId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("D");
}
