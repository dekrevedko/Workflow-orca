namespace OrcaCore.Core.Definitions;

/// <summary>
/// An ordered, immutable list of child nodes executed in order (CR-003).
/// </summary>
public sealed record SequenceNode(IReadOnlyList<DefinitionNode> Steps) : DefinitionNode
{
    public IReadOnlyList<DefinitionNode> Steps { get; } = [.. Steps];
}
