namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class DefinitionAlreadyRegisteredException : WorkflowDefinitionException
{
    public DefinitionAlreadyRegisteredException(
        string definitionId,
        string requestedVersion,
        string registeredVersion,
        string message)
        : base(message)
    {
        DefinitionId = definitionId;
        RequestedVersion = requestedVersion;
        RegisteredVersion = registeredVersion;
    }

    public string DefinitionId { get; }

    public string RequestedVersion { get; }

    public string RegisteredVersion { get; }
}
