namespace OrcaCore.Runtime.Engine.Exceptions;

public abstract class WorkflowRoutingException : WorkflowEngineException
{
    protected WorkflowRoutingException(string message)
        : base(message)
    {
    }
}
