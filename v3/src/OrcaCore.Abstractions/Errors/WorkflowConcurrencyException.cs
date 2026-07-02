namespace OrcaCore.Abstractions.Errors;

/// <summary>Raised when an optimistic-concurrency check on instance version/epoch fails (CR-022).</summary>
public sealed class WorkflowConcurrencyException : OrcaCoreException
{
    public WorkflowConcurrencyException(string message)
        : base(message)
    {
    }

    public WorkflowConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
