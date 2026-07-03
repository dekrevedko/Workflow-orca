namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Immutable version number bound with <see cref="DefinitionId"/> to identify a definition
/// revision (CR-022).
/// </summary>
public readonly record struct DefinitionVersion(int Value)
{
    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
