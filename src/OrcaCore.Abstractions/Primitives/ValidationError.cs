namespace OrcaCore.Abstractions.Primitives;

/// <summary>
/// Describes one build-time validation error.
/// </summary>
public sealed record ValidationError(
    string Code,
    string Message,
    string? Path = null,
    string? RelatedPath = null);
