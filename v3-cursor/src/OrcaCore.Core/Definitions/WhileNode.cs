namespace OrcaCore.Core.Definitions;

/// <summary>
/// Repeats <paramref name="Body"/> while <paramref name="Condition"/> holds.
/// <paramref name="Condition"/> MUST be pure/deterministic (CR-012, NF-020).
/// </summary>
internal sealed record WhileNode(Func<object?, bool> Condition, SequenceNode Body) : DefinitionNode;
