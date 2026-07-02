namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Represents an invalid workflow definition or instance version operation.
/// </summary>
public sealed class WorkflowVersionException : OrcaCoreException
{
    /// <summary>
    /// Initializes a version exception with a caller-actionable message.
    /// </summary>
    public WorkflowVersionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a version exception with a caller-actionable message and inner cause.
    /// </summary>
    public WorkflowVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
