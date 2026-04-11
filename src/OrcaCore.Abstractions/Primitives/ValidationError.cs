namespace OrcaCore.Abstractions.Primitives;

public readonly record struct ValidationError(string Code, string Message, string? Member = null);
