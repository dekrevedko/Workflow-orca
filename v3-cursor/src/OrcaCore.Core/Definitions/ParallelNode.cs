namespace OrcaCore.Core.Definitions;

/// <summary>An immutable list of named branches executed concurrently (CR-044).</summary>
public sealed record ParallelNode(IReadOnlyList<ParallelBranch> Branches) : DefinitionNode
{
    public IReadOnlyList<ParallelBranch> Branches { get; } = [.. Branches];
}
