namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Represents an expected workflow concurrency conflict.
/// </summary>
public sealed class WorkflowConcurrencyException : OrcaCoreException
{
    /// <summary>
    /// Initializes a concurrency exception with a caller-actionable message.
    /// </summary>
    public WorkflowConcurrencyException(string message)
        : base("WF-LEGACY-CONCURRENCY", message)
    {
    }

    /// <summary>
    /// Initializes a concurrency exception with a caller-actionable message and inner cause.
    /// </summary>
    public WorkflowConcurrencyException(string message, Exception innerException)
        : base("WF-LEGACY-CONCURRENCY", message, innerException)
    {
    }
}
