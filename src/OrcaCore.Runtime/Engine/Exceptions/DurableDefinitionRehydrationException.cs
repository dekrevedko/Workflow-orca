namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class DurableDefinitionRehydrationException(string message, Exception innerException)
    : WorkflowDefinitionException(message, innerException);
