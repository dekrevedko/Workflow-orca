using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Building;

/// <summary>
/// Entry points that select execution mode before workflow capabilities are authored.
/// </summary>
public static class Workflow
{
    /// <summary>
    /// Starts authoring an ephemeral workflow definition.
    /// </summary>
    public static EphemeralWorkflowBuilder<TState> Ephemeral<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return new EphemeralWorkflowBuilder<TState>(definitionId, definitionVersion);
    }

    /// <summary>
    /// Starts authoring a durable workflow definition.
    /// </summary>
    public static DurableWorkflowBuilder<TState> Durable<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return new DurableWorkflowBuilder<TState>(definitionId, definitionVersion);
    }
}
