using OrcaCore.Abstractions.Errors;

namespace OrcaCore;

/// <summary>
/// Represents a workflow authoring or definition validation failure.
/// </summary>
public sealed class WorkflowDefinitionException : OrcaCoreException
{
    private const string DefinitionInvalidCode = "WF-DEFINITION-INVALID";

    internal WorkflowDefinitionException(IReadOnlyList<WorkflowDiagnostic> diagnostics)
        : base(DefinitionInvalidCode, string.Join(Environment.NewLine, diagnostics.Select(x => $"{x.Code}: {x.Message} ({x.Location})")))
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Diagnostics = diagnostics.ToArray();
    }

    /// <summary>Gets the complete immutable diagnostic sequence.</summary>
    public IReadOnlyList<WorkflowDiagnostic> Diagnostics { get; }
}
