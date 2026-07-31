namespace OrcaCore.Abstractions.Providers;

/// <summary>Describes one legacy provider pool requirement.</summary>
public sealed record ResourcePoolRequirement(string PoolName, int Count);
