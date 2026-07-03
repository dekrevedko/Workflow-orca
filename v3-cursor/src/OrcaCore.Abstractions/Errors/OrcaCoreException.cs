namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Base exception for operational failures surfaced through <see cref="Primitives.Result{T}"/>.
/// </summary>
public class OrcaCoreException(string message) : Exception(message);
