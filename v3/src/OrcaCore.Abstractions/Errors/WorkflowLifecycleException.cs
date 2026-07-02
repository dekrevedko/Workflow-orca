namespace OrcaCore.Abstractions.Errors;

/// <summary>Raised when an illegal lifecycle trigger is attempted against the transition table (CR-030).</summary>
public sealed class WorkflowLifecycleException : OrcaCoreException
{
    public WorkflowLifecycleException(string message)
        : base(message)
    {
    }

    public WorkflowLifecycleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
