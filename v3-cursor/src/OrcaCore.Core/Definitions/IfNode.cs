namespace OrcaCore.Core.Definitions;

/// <summary>
/// Conditional branch. <paramref name="Condition"/> MUST be pure/deterministic (CR-012,
/// NF-020) — it is evaluated against business state only, never wall-clock time or I/O.
/// </summary>
internal sealed record IfNode(Func<object?, bool> Condition, SequenceNode Then, SequenceNode Else) : DefinitionNode;
