namespace OrcaCore.Runtime.Engine.Exceptions;

public abstract class WorkflowDefinitionException : WorkflowEngineException
{
    protected WorkflowDefinitionException(string message)
        : base(message)
    {
    }

    protected WorkflowDefinitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
