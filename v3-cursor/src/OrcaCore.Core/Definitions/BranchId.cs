namespace OrcaCore.Core.Definitions;

/// <summary>
/// Stable identity of a <see cref="ParallelNode"/> branch (ordinal + author-chosen name).
/// Wait isolation (T1-08/T1-12) depends on this identity remaining stable across replays.
/// </summary>
public readonly record struct BranchId(int Ordinal, string Name);
