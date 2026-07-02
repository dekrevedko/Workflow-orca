namespace OrcaCore.Abstractions.Ids;

/// <summary>Author-chosen, stable name identifying a workflow definition (CR-004).</summary>
public readonly record struct DefinitionId(string Value)
{
    public string Value { get; } = !string.IsNullOrWhiteSpace(Value)
        ? Value
        : throw new ArgumentException("DefinitionId must not be empty or whitespace.", nameof(Value));

    public override string ToString() => Value;
}
