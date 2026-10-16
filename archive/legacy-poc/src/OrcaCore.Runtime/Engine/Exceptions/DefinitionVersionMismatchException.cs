namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class DefinitionVersionMismatchException : WorkflowDefinitionException
{
    public DefinitionVersionMismatchException(
        string definitionId,
        string expectedVersion,
        string actualVersion,
        string? instanceId = null)
        : base(instanceId is null
            ? $"Definition version mismatch for '{definitionId}': expected '{expectedVersion}' but found '{actualVersion}'."
            : $"Definition version mismatch for instance '{instanceId}' and definition '{definitionId}': expected '{expectedVersion}' but found '{actualVersion}'.")
    {
        DefinitionId = definitionId;
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
        InstanceId = instanceId;
    }

    public string DefinitionId { get; }

    public string ExpectedVersion { get; }

    public string ActualVersion { get; }

    public string? InstanceId { get; }
}
