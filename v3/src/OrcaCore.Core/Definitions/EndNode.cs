namespace OrcaCore.Core.Definitions;

/// <summary>
/// Terminates a definition path. An optional named outcome (CR-008) is recorded as metadata
/// only — it never introduces a new lifecycle status.
/// </summary>
public sealed record EndNode(string? OutcomeName) : DefinitionNode;
