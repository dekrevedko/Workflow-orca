namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Represents an illegal workflow lifecycle trigger or transition.
/// </summary>
public sealed class WorkflowLifecycleException : OrcaCoreException
{
    /// <summary>
    /// Initializes a lifecycle exception with a caller-actionable message.
    /// </summary>
    public WorkflowLifecycleException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a lifecycle exception with a caller-actionable message and inner cause.
    /// </summary>
    public WorkflowLifecycleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
