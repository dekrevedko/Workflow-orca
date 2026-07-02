namespace OrcaCore.Abstractions.Errors;

/// <summary>Raised for build/validation failures in a workflow definition (CR-002).</summary>
public sealed class WorkflowDefinitionException : OrcaCoreException
{
    public WorkflowDefinitionException(string message)
        : base(message)
    {
    }

    public WorkflowDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
