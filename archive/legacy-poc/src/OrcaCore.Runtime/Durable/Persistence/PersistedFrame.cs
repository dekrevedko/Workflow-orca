namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedFrame(
    PersistedFrameKind Kind,
    string NodePath,
    int Index,
    string? ScopeId);
