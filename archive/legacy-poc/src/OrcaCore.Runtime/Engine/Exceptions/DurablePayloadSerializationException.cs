namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class DurablePayloadSerializationException(string message) : WorkflowDefinitionException(message);
