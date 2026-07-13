namespace OrcaCore.Runtime.Engine.Exceptions;

public abstract class WorkflowStoreException : WorkflowEngineException
{
    protected WorkflowStoreException(string message)
        : base(message)
    {
    }
}
