namespace OrcaCore.Core.Definitions;

/// <summary>
/// Closed hierarchy of definition graph nodes (CR-003). Pure data — no execution logic.
/// Container nodes hold child <see cref="SequenceNode"/>s; the interpreter (T1-05) walks
/// the tree uniformly.
/// </summary>
public abstract record DefinitionNode
{
    private protected DefinitionNode()
    {
    }
}
