using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// Fluent management query entry point (MG-001: scope → filter → terminal), reached via
/// <see cref="EphemeralWorkflowEngine.Query"/>. Scoping methods have no side effects; the
/// resulting <see cref="ManagementQueryScope"/> adds constrained filtering (MG-002) and terminal
/// snapshot/statistics operations (MG-005: every result is an immutable snapshot or copy, never
/// a live runtime object).
/// </summary>
public sealed class ManagementQueryRoot
{
    private readonly IInstanceRegistry instanceRegistry;

    internal ManagementQueryRoot(IInstanceRegistry instanceRegistry)
    {
        this.instanceRegistry = instanceRegistry;
    }

    /// <summary>Engine-wide scope: every currently-registered instance, across all definitions.</summary>
    public ManagementQueryScope All() => new(instanceRegistry, definitionId: null, instanceId: null);

    /// <summary>Definition-scoped selection (MG-001): only instances of <paramref name="definitionId"/>.</summary>
    public ManagementQueryScope ForDefinition(DefinitionId definitionId) => new(instanceRegistry, definitionId, instanceId: null);

    /// <summary>Instance-scoped selection (MG-001): at most the single instance identified by <paramref name="instanceId"/>.</summary>
    public ManagementQueryScope Instance(InstanceId instanceId) => new(instanceRegistry, definitionId: null, instanceId);
}
