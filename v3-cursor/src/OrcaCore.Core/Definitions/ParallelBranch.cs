namespace OrcaCore.Core.Definitions;

/// <summary>A single named branch of a <see cref="ParallelNode"/>.</summary>
public sealed record ParallelBranch(BranchId Id, SequenceNode Body);
