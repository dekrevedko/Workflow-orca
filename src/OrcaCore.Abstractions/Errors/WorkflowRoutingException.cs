namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Represents a workflow event routing failure that the caller can correct.
/// </summary>
public sealed class WorkflowRoutingException : OrcaCoreException
{
    /// <summary>
    /// Initializes a routing exception with a caller-actionable message.
    /// </summary>
    public WorkflowRoutingException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a routing exception with a caller-actionable message and inner cause.
    /// </summary>
    public WorkflowRoutingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
