namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class DurablePayloadDeserializationException(string message) : WorkflowDefinitionException(message);
