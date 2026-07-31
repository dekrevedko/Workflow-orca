using System.Text.RegularExpressions;

namespace OrcaCore;

/// <summary>
/// Selects the execution semantics fixed by a workflow definition.
/// </summary>
public enum WorkflowMode
{
    /// <summary>Executes in the current process without recovery.</summary>
    Ephemeral,

    /// <summary>Persists execution facts for restart and host replacement.</summary>
    Durable
}

/// <summary>
/// Identifies the compiler-observable structure of a workflow definition.
/// </summary>
public sealed class DefinitionFingerprint : IEquatable<DefinitionFingerprint>
{
    internal DefinitionFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque canonical fingerprint value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public bool Equals(DefinitionFingerprint? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DefinitionFingerprint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Identifies the fixed-codec bytes of one detached value.
/// </summary>
public sealed class PayloadFingerprint : IEquatable<PayloadFingerprint>
{
    internal PayloadFingerprint(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the opaque canonical fingerprint value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public bool Equals(PayloadFingerprint? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PayloadFingerprint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Locates one authored workflow or DAG element in canonical graph order.
/// </summary>
public sealed partial class AuthoredLocation : IEquatable<AuthoredLocation>
{
    internal AuthoredLocation(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!CanonicalValue().IsMatch(value))
        {
            throw new ArgumentException("Value is not a canonical authored location.", nameof(value));
        }

        Value = value;
    }

    [GeneratedRegex(
        "^(?:workflow|dag):\\$(?:/(?:n:[0-9]{8}|if:(?:true|false)|while:body|parallel:[0-9]{8}|foreach:body|lease:body|dag-node:[0-9]{8}))*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalValue();

    /// <summary>Gets the canonical authored-location value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public bool Equals(AuthoredLocation? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AuthoredLocation other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// Classifies a workflow diagnostic without relying on message wording.
/// </summary>
public enum WorkflowDiagnosticSeverity
{
    /// <summary>The definition remains valid.</summary>
    Warning,

    /// <summary>The definition cannot be built.</summary>
    Error
}

/// <summary>
/// One immutable workflow authoring or compilation diagnostic.
/// </summary>
public sealed class WorkflowDiagnostic
{
    internal WorkflowDiagnostic(
        string code,
        WorkflowDiagnosticSeverity severity,
        AuthoredLocation location,
        IReadOnlyList<AuthoredLocation> relatedLocations,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(relatedLocations);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var descriptor = WorkflowDiagnosticCatalog.Require(code);
        if (descriptor.Severity != severity)
        {
            throw new ArgumentException(
                $"Diagnostic '{code}' must use severity '{descriptor.Severity}'.",
                nameof(severity));
        }

        Code = code;
        Severity = severity;
        Location = location;
        RelatedLocations = relatedLocations
            .Distinct()
            .OrderBy(x => x.Value, StringComparer.Ordinal)
            .ToArray();
        Message = message;
    }

    /// <summary>Gets the stable machine-readable diagnostic code.</summary>
    public string Code { get; }

    /// <summary>Gets the diagnostic severity.</summary>
    public WorkflowDiagnosticSeverity Severity { get; }

    /// <summary>Gets the primary authored location.</summary>
    public AuthoredLocation Location { get; }

    /// <summary>Gets ordered related authored locations.</summary>
    public IReadOnlyList<AuthoredLocation> RelatedLocations { get; }

    /// <summary>Gets the human-readable diagnostic detail.</summary>
    public string Message { get; }
}

/// <summary>
/// Represents a built value accompanied by immutable authoring diagnostics.
/// </summary>
public sealed class Validation<T>
{
    private readonly T? value;

    internal Validation(T? value, IReadOnlyList<WorkflowDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.value = value;
        Diagnostics = diagnostics
            .OrderBy(x => x.Location.Value, StringComparer.Ordinal)
            .ThenBy(x => x.Code, StringComparer.Ordinal)
            .ToArray();
        IsValid = value is not null && Diagnostics.All(x => x.Severity != WorkflowDiagnosticSeverity.Error);
    }

    /// <summary>Gets whether a value is available and no error diagnostic exists.</summary>
    public bool IsValid { get; }

    /// <summary>Gets the immutable ordered diagnostic sequence.</summary>
    public IReadOnlyList<WorkflowDiagnostic> Diagnostics { get; }

    /// <summary>Projects the built value when validation succeeded.</summary>
    public bool TryGetValue(out T? result)
    {
        result = IsValid ? value : default;
        return IsValid;
    }
}
