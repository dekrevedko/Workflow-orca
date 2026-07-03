namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Captured failure information for a failed instance (CR-014): error type, message, the
/// definition-graph path of the step that failed, and when the failure was observed. Internal
/// only — public callers see the projected <see cref="Abstractions.Instances.WorkflowInstanceSnapshot.ErrorSummary"/>.
/// </summary>
internal sealed record StepErrorDetails(string ErrorType, string Message, string StepPath, DateTimeOffset OccurredAt)
{
    /// <summary>Human-readable one-line summary suitable for the snapshot's error summary.</summary>
    internal string Summary => $"{ErrorType}: {Message} (at {StepPath})";
}
