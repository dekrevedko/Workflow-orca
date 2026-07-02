namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Raised when correlation-targeted or fanout event routing finds no match or more than one
/// match (EV-012). The message states which case occurred so the caller can act.
/// </summary>
public sealed class WorkflowRoutingException : OrcaCoreException
{
    public WorkflowRoutingException(string message)
        : base(message)
    {
    }

    public WorkflowRoutingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
