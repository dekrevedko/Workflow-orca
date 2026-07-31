using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Durable.Versioning;

internal static class DurableVersionCompatibility
{
    internal static void EnsureCompatible(
        DefinitionId existingDefinitionId,
        DefinitionVersion existingDefinitionVersion,
        DefinitionId requestedDefinitionId,
        DefinitionVersion requestedDefinitionVersion)
    {
        if (existingDefinitionId.Equals(requestedDefinitionId) &&
            existingDefinitionVersion == requestedDefinitionVersion)
        {
            return;
        }

        throw new WorkflowVersionException(
            $"The requested definition version is incompatible with the existing durable instance. " +
            $"existing={existingDefinitionId}/{existingDefinitionVersion}; " +
            $"requested={requestedDefinitionId}/{requestedDefinitionVersion}. Start a new instance or route to the bound version.");
    }
}
