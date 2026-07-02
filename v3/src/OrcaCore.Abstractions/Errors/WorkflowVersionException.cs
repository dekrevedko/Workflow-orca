namespace OrcaCore.Abstractions.Errors;

/// <summary>Raised for definition-version binding failures at instance start (DU-041).</summary>
public sealed class WorkflowVersionException : OrcaCoreException
{
    public WorkflowVersionException(string message)
        : base(message)
    {
    }

    public WorkflowVersionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
