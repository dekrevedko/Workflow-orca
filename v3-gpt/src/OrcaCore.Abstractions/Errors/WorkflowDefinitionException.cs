namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Represents a workflow authoring or definition validation failure.
/// </summary>
public sealed class WorkflowDefinitionException : OrcaCoreException
{
    /// <summary>
    /// Initializes a definition exception with a caller-actionable message.
    /// </summary>
    public WorkflowDefinitionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a definition exception with a caller-actionable message and inner cause.
    /// </summary>
    public WorkflowDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
