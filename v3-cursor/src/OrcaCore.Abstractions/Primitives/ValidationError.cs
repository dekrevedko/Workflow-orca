namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// A single build-time validation failure.
/// </summary>
public sealed record ValidationError(string Code, string Message, string? Path = null);
