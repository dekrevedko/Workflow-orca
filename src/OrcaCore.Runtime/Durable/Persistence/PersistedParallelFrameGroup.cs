namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedParallelFrameGroup(
    IReadOnlyDictionary<string, PersistedExecutionPath> BranchPaths);
