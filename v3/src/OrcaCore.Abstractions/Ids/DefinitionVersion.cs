namespace OrcaCore.Abstractions.Ids;

/// <summary>Author-assigned, monotonically increasing version number of a definition (CR-004).</summary>
public readonly record struct DefinitionVersion(int Value)
{
    public int Value { get; } = Value > 0
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), Value, "DefinitionVersion must be positive.");

    public override string ToString() => Value.ToString();
}
