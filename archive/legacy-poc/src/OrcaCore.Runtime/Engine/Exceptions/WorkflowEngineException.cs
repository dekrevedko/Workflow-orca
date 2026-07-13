namespace OrcaCore.Runtime.Engine.Exceptions;

public abstract class WorkflowEngineException : InvalidOperationException
{
    protected WorkflowEngineException(string message)
        : base(message)
    {
    }

    protected WorkflowEngineException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
