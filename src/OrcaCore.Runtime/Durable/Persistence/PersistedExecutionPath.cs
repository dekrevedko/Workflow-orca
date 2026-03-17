namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedExecutionPath(
    string? BranchId,
    IReadOnlyList<PersistedFrame> Frames);
