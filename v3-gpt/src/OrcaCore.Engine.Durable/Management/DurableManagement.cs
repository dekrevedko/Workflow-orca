using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Management;

/// <summary>
/// Provides the root management surface for durable workflow projections.
/// </summary>
public sealed class DurableManagement(IWorkflowProjectionStore projectionStore)
{
    /// <summary>
    /// Selects all durable instances visible in projections.
    /// </summary>
    public DurableManagementQuery All()
    {
        return new DurableManagementQuery(projectionStore, WorkflowProjectionQuery.All);
    }

    /// <summary>
    /// Selects durable instances for one definition.
    /// </summary>
    public DurableManagementQuery ForDefinition(DefinitionId definitionId)
    {
        return All().Where(instance => instance.DefinitionId == definitionId);
    }

    /// <summary>
    /// Selects one durable instance by id.
    /// </summary>
    public DurableManagementQuery Instance(InstanceId instanceId)
    {
        return All().Where(instance => instance.InstanceId == instanceId);
    }
}
